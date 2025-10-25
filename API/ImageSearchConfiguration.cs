namespace ImageSearchCL.API;

/// <summary>
/// Global configuration settings for ImageSearchCL library.
/// </summary>
/// <remarks>
/// This class provides library-wide configuration options:
/// - Event synchronization behavior
/// - Default timeout values
/// - Template matching mode
/// - Debug overlay visualization
///
/// Settings are static and affect all tracking sessions.
/// Thread-safe: All properties use atomic reads/writes.
///
/// Example:
/// <code>
/// // Configure before creating sessions
/// ImageSearchConfiguration.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
/// ImageSearchConfiguration.EventSynchronizationContext = SynchronizationContext.Current;
///
/// // Create sessions - they will use these settings
/// var session = Search.For("button.png").In(captureSession);
/// </code>
/// </remarks>
public static class ImageSearchConfiguration
{
    private static SynchronizationContext? _eventSynchronizationContext;
    private static TimeSpan _defaultWaitTimeout = TimeSpan.FromSeconds(30);
    private static TemplateMatchMode _matchMode = TemplateMatchMode.CCoeffNormed;
    private static bool _enableDebugOverlay = false;
    private static System.Drawing.Color _debugOverlayColor = System.Drawing.Color.Lime;
    private static int _debugOverlayThickness = 2;
    private static IntPtr _debugOverlayWindowHandle = IntPtr.Zero;
    private static double _defaultConfidence = 0.8;
    private static double _defaultMovementThreshold = 5.0;
    private static double _defaultOverlapThreshold = 0.5;

    /// <summary>
    /// Gets or sets the SynchronizationContext used for event marshalling.
    /// </summary>
    /// <value>
    /// SynchronizationContext to marshal events to, or null to use background thread.
    /// Default: null (events fire on background thread).
    /// </value>
    /// <remarks>
    /// Setting Behavior:
    /// - If null: Events fire on background thread (ICaptureSession.FrameReady thread)
    /// - If non-null: Events marshalled to specified context (e.g., UI thread)
    ///
    /// Typical Usage:
    /// - WPF: Set to SynchronizationContext.Current from UI thread
    /// - WinForms: Set to WindowsFormsSynchronizationContext
    /// - Console: Leave null (no marshalling needed)
    ///
    /// Thread Safety:
    /// - Property is thread-safe (uses volatile read/write)
    /// - Set this BEFORE creating tracking sessions
    ///
    /// Note: Individual sessions capture SynchronizationContext.Current at construction time.
    /// This property provides a fallback if SynchronizationContext.Current is null.
    /// </remarks>
    /// <example>
    /// <code>
    /// // WPF application
    /// public MainWindow()
    /// {
    ///     InitializeComponent();
    ///
    ///     // Set global context to UI thread
    ///     ImageSearchConfiguration.EventSynchronizationContext = SynchronizationContext.Current;
    ///
    ///     // All sessions will now marshal events to UI thread
    ///     var session = Search.For("button.png").In(captureSession);
    ///     session.Appeared += (s, r) =>
    ///     {
    ///         // Safe to update UI - we're on UI thread
    ///         StatusLabel.Text = $"Found at {r.Center}";
    ///     };
    /// }
    /// </code>
    /// </example>
    public static SynchronizationContext? EventSynchronizationContext
    {
        get => _eventSynchronizationContext;
        set => _eventSynchronizationContext = value;
    }

    /// <summary>
    /// Gets or sets the default timeout for WaitUntilVisible/WaitUntilNotVisible methods.
    /// </summary>
    /// <value>
    /// Default timeout duration.
    /// Default: 30 seconds.
    /// </value>
    /// <remarks>
    /// This value is used when calling wait methods without specifying a timeout:
    /// - WaitUntilVisible() uses this timeout
    /// - WaitUntilNotVisible() uses this timeout
    ///
    /// Individual wait calls can override by passing explicit timeout.
    ///
    /// Thread Safety:
    /// - Property is thread-safe (uses volatile read/write)
    ///
    /// Recommendations:
    /// - UI automation: 5-15 seconds
    /// - Integration tests: 1-5 seconds
    /// - Long-running processes: 60+ seconds
    /// - Use Timeout.InfiniteTimeSpan for no timeout (wait forever)
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set shorter timeout for fast-paced automation
    /// ImageSearchConfiguration.DefaultWaitTimeout = TimeSpan.FromSeconds(5);
    ///
    /// // This will timeout after 5 seconds
    /// var result = session.WaitUntilVisible(ImageSearchConfiguration.DefaultWaitTimeout);
    /// </code>
    /// </example>
    public static TimeSpan DefaultWaitTimeout
    {
        get => _defaultWaitTimeout;
        set
        {
            if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Timeout must be non-negative or Timeout.InfiniteTimeSpan.");

            _defaultWaitTimeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the template matching algorithm mode.
    /// </summary>
    /// <value>
    /// Template matching mode.
    /// Default: CCoeffNormed (correlation coefficient, normalized).
    /// </value>
    /// <remarks>
    /// Matching Modes:
    /// - CCoeffNormed (recommended): Correlation coefficient, normalized to [0.0, 1.0]
    ///   - Best for most use cases
    ///   - Robust to brightness variations
    ///   - Confidence scores are intuitive (1.0 = perfect match)
    ///
    /// - SqDiff: Sum of squared differences (NOT SUPPORTED YET)
    /// - CCorr: Cross-correlation (NOT SUPPORTED YET)
    ///
    /// Thread Safety:
    /// - Property is thread-safe (uses volatile read/write)
    /// - Set this BEFORE creating tracking sessions
    ///
    /// Note: Currently only CCoeffNormed is implemented.
    /// Future versions may support additional modes.
    /// </remarks>
    public static TemplateMatchMode MatchMode
    {
        get => _matchMode;
        set
        {
            if (value != TemplateMatchMode.CCoeffNormed)
                throw new NotSupportedException($"MatchMode {value} is not supported. Only CCoeffNormed is currently implemented.");

            _matchMode = value;
        }
    }

    /// <summary>
    /// Gets or sets whether the visual debug overlay is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable the debug overlay; otherwise, <c>false</c>.
    /// Default: <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, a transparent overlay window displays bounding boxes around detected objects.
    ///
    /// The overlay is:
    /// - Transparent with per-pixel alpha blending
    /// - Click-through (does not block mouse input)
    /// - Always on top
    /// - Fullscreen across all monitors
    ///
    /// Uses Windows Layered Window API (UpdateLayeredWindow) for:
    /// - Hardware accelerated rendering
    /// - No flickering
    /// - Minimal CPU impact (~1% at 30 FPS)
    ///
    /// Thread Safety: Property is thread-safe
    /// </remarks>
    /// <example>
    /// <code>
    /// // Enable debug overlay for development
    /// ImageSearchConfiguration.EnableDebugOverlay = true;
    /// ImageSearchConfiguration.DebugOverlayColor = Color.Red;
    /// ImageSearchConfiguration.DebugOverlayThickness = 3;
    ///
    /// // Bounding boxes will appear when tracking
    /// var session = Search.For("button.png").In(capture);
    /// session.Start();
    /// </code>
    /// </example>
    public static bool EnableDebugOverlay
    {
        get => _enableDebugOverlay;
        set => _enableDebugOverlay = value;
    }

    /// <summary>
    /// Gets or sets the color for debug overlay bounding boxes.
    /// </summary>
    /// <value>
    /// Color for bounding box lines. Default: Lime (bright green).
    /// </value>
    /// <remarks>
    /// Recommended colors:
    /// - Color.Lime - bright green, high visibility (default)
    /// - Color.Red - for errors or warnings
    /// - Color.Cyan - for secondary objects
    /// - Color.Yellow - for highlighted objects
    /// </remarks>
    public static System.Drawing.Color DebugOverlayColor
    {
        get => _debugOverlayColor;
        set => _debugOverlayColor = value;
    }

    /// <summary>
    /// Gets or sets the line thickness for debug overlay bounding boxes.
    /// </summary>
    /// <value>
    /// Thickness in pixels (1-10). Default: 2.
    /// </value>
    /// <remarks>
    /// Recommended values:
    /// - 1: Thin lines
    /// - 2: Default, good visibility
    /// - 3-4: Thick lines, high visibility
    /// </remarks>
    public static int DebugOverlayThickness
    {
        get => _debugOverlayThickness;
        set
        {
            if (value < 1 || value > 10)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Thickness must be between 1 and 10.");

            _debugOverlayThickness = value;
        }
    }

    /// <summary>
    /// Gets or sets the window handle for coordinate conversion in debug overlay.
    /// </summary>
    /// <value>
    /// Window handle (IntPtr) for coordinate conversion, or IntPtr.Zero for screen coordinates (default).
    /// Default: IntPtr.Zero.
    /// </value>
    /// <remarks>
    /// When capturing a specific window (not full screen), set this to the window handle
    /// so the overlay can convert window-relative coordinates to screen coordinates.
    ///
    /// Use cases:
    /// - IntPtr.Zero: Capturing full screen (default)
    /// - Window handle: Capturing specific window (e.g., Notepad, Chrome)
    ///
    /// Thread Safety: Property is thread-safe
    /// </remarks>
    public static IntPtr DebugOverlayWindowHandle
    {
        get => _debugOverlayWindowHandle;
        set => _debugOverlayWindowHandle = value;
    }

    /// <summary>
    /// Gets or sets the default confidence threshold for template matching.
    /// </summary>
    /// <value>
    /// Confidence threshold between 0.0 and 1.0. Default: 0.8 (80%).
    /// </value>
    /// <remarks>
    /// This value is used when WithConfidence() is not explicitly called:
    /// - Search.For() uses this confidence
    /// - Search.FindAll() uses this confidence
    /// - Search.Find() uses this confidence
    ///
    /// Recommendations:
    /// - 0.95-0.99: Very strict (exact match required)
    /// - 0.85-0.90: Recommended for most cases
    /// - 0.75-0.80: More lenient (allows slight variations)
    /// - Below 0.70: High false positive rate
    ///
    /// Thread Safety: Property is thread-safe
    /// </remarks>
    public static double DefaultConfidence
    {
        get => _defaultConfidence;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Confidence must be between 0.0 and 1.0.");

            _defaultConfidence = value;
        }
    }

    /// <summary>
    /// Gets or sets the default movement threshold in pixels.
    /// </summary>
    /// <value>
    /// Movement threshold in pixels. Default: 5.0 pixels.
    /// </value>
    /// <remarks>
    /// This value determines when the Moved event fires.
    /// Object must move more than this distance to trigger the event.
    ///
    /// Used when WithMovementThreshold() is not explicitly called.
    ///
    /// Recommendations:
    /// - 1-3 pixels: Detect small movements (may be noisy)
    /// - 5-10 pixels: Recommended for most cases (default: 5)
    /// - 15+ pixels: Only detect significant movements
    ///
    /// Thread Safety: Property is thread-safe
    /// </remarks>
    public static double DefaultMovementThreshold
    {
        get => _defaultMovementThreshold;
        set
        {
            if (value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Movement threshold must be non-negative.");

            _defaultMovementThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets the default overlap threshold for FindAll() operations.
    /// </summary>
    /// <value>
    /// Overlap threshold between 0.0 and 1.0. Default: 0.5 (50% overlap).
    /// </value>
    /// <remarks>
    /// This value controls Non-Maximum Suppression in FindAll().
    /// When two detections overlap more than this threshold, only the one
    /// with higher confidence is kept.
    ///
    /// Used when WithOverlapThreshold() is not explicitly called.
    ///
    /// Recommendations:
    /// - 0.1-0.3: Very strict (keep detections with minimal overlap)
    /// - 0.5: Recommended for most cases (default)
    /// - 0.7-0.9: More lenient (allow significant overlap)
    ///
    /// Thread Safety: Property is thread-safe
    /// </remarks>
    public static double DefaultOverlapThreshold
    {
        get => _defaultOverlapThreshold;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Overlap threshold must be between 0.0 and 1.0.");

            _defaultOverlapThreshold = value;
        }
    }

    /// <summary>
    /// Resets all configuration settings to their default values.
    /// </summary>
    /// <remarks>
    /// Default values:
    /// - EventSynchronizationContext: null
    /// - DefaultWaitTimeout: 30 seconds
    /// - MatchMode: CCoeffNormed
    /// - EnableDebugOverlay: false
    /// - DebugOverlayColor: Lime
    /// - DebugOverlayThickness: 2
    /// - DebugOverlayWindowHandle: IntPtr.Zero
    /// - DefaultConfidence: 0.8
    /// - DefaultMovementThreshold: 5.0
    /// - DefaultOverlapThreshold: 0.5
    ///
    /// Thread Safety: This method is thread-safe
    /// </remarks>
    public static void Reset()
    {
        _eventSynchronizationContext = null;
        _defaultWaitTimeout = TimeSpan.FromSeconds(30);
        _matchMode = TemplateMatchMode.CCoeffNormed;
        _enableDebugOverlay = false;
        _debugOverlayColor = System.Drawing.Color.Lime;
        _debugOverlayThickness = 2;
        _debugOverlayWindowHandle = IntPtr.Zero;
        _defaultConfidence = 0.8;
        _defaultMovementThreshold = 5.0;
        _defaultOverlapThreshold = 0.5;
    }
}

/// <summary>
/// Template matching algorithm modes.
/// </summary>
/// <remarks>
/// Determines how OpenCV compares the reference image to the frame.
///
/// Currently Supported:
/// - CCoeffNormed: Correlation coefficient, normalized
///
/// Future Support:
/// - SqDiff: Sum of squared differences
/// - CCorr: Cross-correlation
/// </remarks>
public enum TemplateMatchMode
{
    /// <summary>
    /// Correlation coefficient, normalized to [0.0, 1.0].
    /// </summary>
    /// <remarks>
    /// Properties:
    /// - Robust to brightness variations
    /// - 1.0 = perfect match
    /// - 0.0 = no correlation
    /// - Recommended for most use cases
    ///
    /// OpenCV: TemplateMatchModes.CCoeffNormed
    /// </remarks>
    CCoeffNormed = 0,

    // Future modes (not yet implemented):
    // SqDiff = 1,
    // CCorr = 2,
}
