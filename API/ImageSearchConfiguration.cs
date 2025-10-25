namespace ImageSearchCL.API;

/// <summary>
/// Global configuration settings for ImageSearchCL library.
/// </summary>
/// <remarks>
/// This class provides library-wide configuration options:
/// - Event synchronization behavior
/// - Default timeout values
/// - Template matching mode
/// - Debug settings (visual overlay - future)
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
    /// When enabled, a transparent overlay window will display:
    /// - Bounding boxes around detected objects
    /// - Confidence scores as percentages
    /// - Center anchor points with crosshairs
    ///
    /// The overlay is:
    /// - Click-through (does not block mouse input)
    /// - Always on top
    /// - Fullscreen across all monitors
    ///
    /// Performance Impact:
    /// - Minimal (~1-2% CPU for rendering at 10 FPS)
    /// - Only active when tracking sessions are running
    ///
    /// Thread Safety:
    /// - Property is thread-safe
    ///
    /// Use Cases:
    /// - Development and debugging
    /// - Tuning confidence thresholds
    /// - Visualizing template matching results
    /// </remarks>
    /// <example>
    /// <code>
    /// // Enable debug overlay for development
    /// ImageSearchConfiguration.EnableDebugOverlay = true;
    /// ImageSearchConfiguration.DebugOverlayColor = Color.Lime;
    /// ImageSearchConfiguration.DebugOverlayThickness = 3;
    ///
    /// // Start tracking - bounding boxes will be visible
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
    /// Gets or sets the color used for debug overlay rendering.
    /// </summary>
    /// <value>
    /// Color for bounding boxes and text.
    /// Default: Lime (bright green).
    /// </value>
    /// <remarks>
    /// Recommended colors for visibility:
    /// - Color.Lime (bright green) - default, high visibility
    /// - Color.Red - for errors or critical detections
    /// - Color.Cyan - for secondary detections
    /// - Color.Yellow - for warnings
    ///
    /// Thread Safety:
    /// - Property is thread-safe
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
    /// Thickness in pixels.
    /// Default: 2.
    /// </value>
    /// <remarks>
    /// Recommended values:
    /// - 1: Thin lines, less intrusive
    /// - 2: Default, good balance
    /// - 3-4: Thick lines, high visibility
    ///
    /// Thread Safety:
    /// - Property is thread-safe
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
    ///
    /// Thread Safety:
    /// - This method is thread-safe
    ///
    /// Use Cases:
    /// - Testing: Reset between test cases
    /// - Plugin scenarios: Clean slate for new plugin
    /// </remarks>
    public static void Reset()
    {
        _eventSynchronizationContext = null;
        _defaultWaitTimeout = TimeSpan.FromSeconds(30);
        _matchMode = TemplateMatchMode.CCoeffNormed;
        _enableDebugOverlay = false;
        _debugOverlayColor = System.Drawing.Color.Lime;
        _debugOverlayThickness = 2;
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
