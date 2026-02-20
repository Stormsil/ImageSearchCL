# CLAUDE.md

This file contains essential information for working in this codebase. Read this first before making changes.

## Build Commands

```bash
# Build entire solution
dotnet build ImageSearchCL.sln

# Build with specific configuration
dotnet build -c Release
dotnet build -c Debug

# Build for specific platform
dotnet build -p:Platform=x64

# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore

# Create NuGet package
dotnet pack -c Release
```

**Note**: Test projects mentioned in `InternalsVisibleTo` (ImageSearchCL.Tests, ImageSearchCL.Benchmarks) are not currently in the solution.

## High-Level Architecture

### Project Overview
ImageSearchCL is a **real-time object tracking library** for .NET 8 Windows applications. It uses OpenCV template matching to track UI elements with event-driven notifications and fluent API design.

**Performance Targets:**
- ≥30 FPS frame processing (≤33ms per frame)
- <50ms detection latency from capture to event
- <5% CPU usage when object is stationary
- <1KB memory overhead per session

### Three-Layer Architecture

```
┌─────────────────────────────────────────┐
│         API Layer (Public)              │
│  Search, IObjectSearch, FindResult      │
│  Fluent builders, Configuration         │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│      Core Layer (Internal)              │
│  TrackingSession - State machine        │
│  Event orchestration, Lifecycle         │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│   Infrastructure Layer (Internal)       │
│  TemplateMatchingEngine (OpenCV)        │
│  FrameQueue (lock-free buffering)       │
│  DebugOverlay (visual debugging)        │
└─────────────────────────────────────────┘
```

### Design Patterns

**Fluent Builder Pattern:**
```csharp
Search.For("button.png")
    .WithConfidence(0.9)
    .WithMovementThreshold(10.0)
    .In(captureSession);
```

**Event-Driven Architecture:**
- `Appeared`: NotVisible → Visible transition
- `Disappeared`: Visible → NotVisible transition
- `Moved`: Center moved ≥ MovementThreshold pixels (default 5px)
- `StateChanged`: All lifecycle transitions

**SynchronizationContext Marshalling:**
- All events automatically marshalled to UI thread
- Captured at `TrackingSession` construction time
- Uses `SynchronizationContext.Post` for thread safety

**Lock-Free Frame Buffering:**
- `FrameQueue` uses atomic swap for single-slot buffer
- Capture thread enqueues without blocking
- Processing thread dequeues asynchronously
- Automatic frame dropping when processing falls behind

**Immutable Configuration:**
- `TrackingConfiguration` is immutable after creation
- Changes require new tracking session
- Ensures thread-safe access without locking

### Threading Model

```
Capture Thread              Processing Thread           UI Thread
     │                             │                         │
     │ FrameReady event           │                         │
     ├──────────────────────────> │                         │
     │ Enqueue (lock-free)        │                         │
     │                             │ Dequeue frame          │
     │                             │ Template matching      │
     │                             │ State comparison       │
     │                             │ RaiseEvent()           │
     │                             ├───────────────────────>│
     │                             │  (via SyncContext)     │
     │                             │                        │ Event handlers
     │                             │                        │ execute safely
```

**Critical Threading Details:**
- **Capture thread**: `ICaptureSession.FrameReady` → `FrameQueue.Enqueue()` (O(1), lock-free)
- **Processing thread**: Background `Task` → `FrameQueue.Dequeue()` → `TemplateMatchingEngine.FindTemplate()`
- **UI thread**: Events delivered via `SynchronizationContext.Post()` (guaranteed UI-safe)
- **State changes**: Protected by `_stateLock` for thread-safe reads/writes

### External Integration

**WindowCaptureCL Dependency:**
- External project located at: `..\..\WindowCaptureCL\WindowCaptureCL.csproj`
- Integrated via adapter project: `ImageSearchCL.WindowCapture`
- Provides screen/window capture capabilities
- NOT included in main NuGet package (adapter pattern allows different capture sources)

## Key Concepts

### Entry Points

**1. One-Time Searches** (synchronous, no events):
```csharp
// Find single best match
var result = ImageSearch.Find("button.png", screenshot, confidence: 0.8);

// Find all matches
var results = ImageSearch.FindAll("icon.png", screenshot, confidence: 0.8);
```

**2. Continuous Tracking** (asynchronous, event-driven):
```csharp
// Single template
using var session = Search.For("button.png").In(captureSession);
session.Appeared += (s, r) => Console.WriteLine($"Found at {r.Center}");
session.Start();

// Multiple templates (finds best match)
using var session = Search.ForAny("btn_normal.png", "btn_hover.png", "btn_disabled.png")
    .WithConfidence(0.85)
    .In(captureSession);
session.Start();
```

**3. Session Lifecycle Interface** (`IObjectSearch`):
- `Start()`: Begin tracking (NotStarted → Running)
- `Pause()`: Temporarily stop (Running → Paused)
- `Resume()`: Continue tracking (Paused → Running)
- `Stop()`: Permanently stop (Running/Paused → Stopped)
- `Dispose()`: Release resources (Any → Disposed)

### State Machine

```
NotStarted ──Start()──> Running ──Pause()──> Paused
                          │                     │
                          │                     │
                          │ <────Resume()───────┘
                          │
                          │
                      Stop() / Dispose()
                          │
                          ▼
                      Stopped ──Dispose()──> Disposed
```

**State Transition Rules:**
- `Start()`: Only from NotStarted
- `Pause()`: Only from Running
- `Resume()`: Only from Paused
- `Stop()`: From Running or Paused (idempotent)
- `Dispose()`: From any state (idempotent)

### Configuration System

**Global Defaults** (static `ImageSearchConfiguration`):
```csharp
ImageSearchConfiguration.DefaultConfidence = 0.8;
ImageSearchConfiguration.DefaultMovementThreshold = 5.0;
ImageSearchConfiguration.EnableDebugOverlay = true;
```

**Per-Session** (immutable `TrackingConfiguration`):
- Set at construction via fluent builder
- Cannot be modified after creation
- Contains: ReferenceImage[], ConfidenceThreshold, MovementThreshold

**Fluent Overrides**:
```csharp
Search.For("target.png")
    .WithConfidence(0.95)        // Override default
    .WithMovementThreshold(2.0)   // Override default
    .In(captureSession);
```

### Event System Details

**Appeared Event:**
- **Trigger**: Confidence rises from below → above threshold
- **Args**: `FindResult` with position, confidence, 9 anchor points
- **Latency**: 30-50ms typical
- **Use**: UI automation, logging, screen markers

**Disappeared Event:**
- **Trigger**: Confidence drops from above → below threshold
- **Args**: Last known `FindResult` before disappearance
- **Latency**: 30-50ms typical
- **Use**: Cleanup, error detection, state management

**Moved Event:**
- **Trigger**: Euclidean distance between old/new center ≥ threshold
- **Args**: `MovedEventArgs` with OldResult, NewResult, Distance
- **Default threshold**: 5 pixels
- **Use**: Tracking, velocity calculation, visual debugging

**StateChanged Event:**
- **Trigger**: All lifecycle transitions
- **Args**: `StateChangedEventArgs` with OldState, NewState, Timestamp
- **Use**: UI updates, logging, resource management

### Blocking Wait Methods

```csharp
// Wait for object to appear (useful for sequential automation)
var result = session.WaitUntilVisible(TimeSpan.FromSeconds(5));
if (result != null)
    Console.WriteLine($"Object appeared at {result.Center}");

// Wait for object to disappear
if (session.WaitUntilNotVisible(TimeSpan.FromSeconds(2)))
    Console.WriteLine("Object disappeared successfully");
```

**Behavior:**
- Thread-safe, can be called from any thread
- Blocks calling thread until condition met or timeout
- Returns immediately if condition already satisfied
- Returns null/false on timeout or if session stopped
- Throws if session not in Running state

### Anchor Points System

Every `FindResult` includes 9 pre-computed anchor points for easy positioning:

```
TopLeft────TopCenter────TopRight
   │                         │
   │                         │
CenterLeft──Center──CenterRight
   │                         │
   │                         │
BottomLeft─BottomCenter─BottomRight
```

**Usage:**
```csharp
session.Appeared += (s, result) =>
{
    // Click center
    Mouse.Click(result.Center);

    // Click top-right corner
    Mouse.Click(result.TopRight);

    // Position relative to bottom
    var offset = result.BottomCenter.Offset(0, 10);
};
```

## OpenCV Integration

### Template Matching Configuration

- **Package**: OpenCvSharp4 v4.11.0.20250507
- **Method**: `MatchTemplate()` with `CCoeffNormed` (normalized correlation coefficient)
- **Confidence Range**: 0.0 to 1.0 (1.0 = perfect pixel match)
- **Multi-Object Detection**: Non-Maximum Suppression (NMS) for `FindAll()`
- **Supported Formats**: 24bppRgb, 32bppArgb, 32bppRgb

### Template Matching Engine

Located in: `Infrastructure/TemplateMatchingEngine.cs`

**Key Methods:**
- `FindTemplate()`: Single best match above threshold
- `FindAllTemplates()`: All matches with NMS overlap filtering

**Performance Characteristics:**
- O(n×m) where n=image pixels, m=template pixels
- Faster for smaller templates
- GPU acceleration NOT currently enabled (CPU-only)

### Bitmap Conversion

- Uses `OpenCvSharp4.Extensions.BitmapConverter`
- Automatic format conversion to CV_8UC3 (3-channel 8-bit)
- Handles transparency by converting to RGB

## Dependencies

```xml
<PackageReference Include="OpenCvSharp4" Version="4.11.0.20250507" />
<PackageReference Include="OpenCvSharp4.Extensions" Version="4.11.0.20250507" />
<PackageReference Include="OpenCvSharp4.runtime.win" Version="4.11.0.20250507" />
<PackageReference Include="System.Drawing.Common" Version="9.0.10" />
```

**External Projects:**
- **WindowCaptureCL**: Screen/window capture (referenced by WindowCapture adapter)
  - Path: `..\..\WindowCaptureCL\WindowCaptureCL.csproj`
  - Not part of solution, integration via adapter pattern

## Important Implementation Notes

### File Organization

```
API/              - Public interfaces (Search, IObjectSearch, FindResult)
Core/             - Business logic (TrackingSession state machine)
Infrastructure/   - Low-level services (TemplateMatchingEngine, FrameQueue, DebugOverlay)
```

**Visibility:**
- API types: `public`
- Core/Infrastructure: `internal` (exposed via InternalsVisibleTo for tests)

### Common Pitfalls

1. **Don't dispose ReferenceImage passed to `Search.For(ReferenceImage)`**
   - Caller retains ownership
   - Only dispose if created with `Search.For(string)` or `Search.For(Bitmap)`

2. **Events are marshalled to SynchronizationContext**
   - If no SyncContext (console app), events fire on background thread
   - For WinForms/WPF, events automatically on UI thread

3. **State machine is strict**
   - `Start()` only works from NotStarted
   - `Pause()` only works from Running
   - Can't restart after `Stop()` - create new session

4. **Movement threshold prevents jitter**
   - Default 5px filters template matching noise
   - Lower values (1-2px) = more sensitive but noisier
   - Higher values (10-20px) = less sensitive but smoother

5. **Confidence threshold tuning**
   - Default 0.8 works for most cases
   - Increase (0.9-0.95) for exact matches with no variation
   - Decrease (0.6-0.75) for templates with lighting/scaling changes
   - Too low (<0.5) risks false positives

### Debug Overlay

- Enabled via `ImageSearchConfiguration.EnableDebugOverlay = true`
- Shows real-time detection rectangles on screen
- Automatically shown/hidden based on active session count
- Implemented as transparent overlay form (Windows Forms)
- Customize via `DebugOverlayColor`, `DebugOverlayThickness`, `DebugOverlayWindowHandle`

## Multi-Template Tracking

When using `Search.ForAny()`:
- All templates tested each frame
- Returns best match (highest confidence) above threshold
- Result is `MultiFindResult` with:
  - `MatchedTemplate`: Which ReferenceImage matched
  - `MatchedTemplateIndex`: Index in original array

**Use Cases:**
- UI states: normal/hover/disabled button variants
- Localization: different language versions of same element
- Themes: light/dark mode variants

```csharp
using var session = Search.ForAny("btn_en.png", "btn_ru.png", "btn_zh.png")
    .In(captureSession);

session.Appeared += (s, r) =>
{
    if (r is MultiFindResult multi)
    {
        Console.WriteLine($"Found button variant #{multi.MatchedTemplateIndex}");
        Console.WriteLine($"Template name: {multi.MatchedTemplate.Name}");
    }
};
```
