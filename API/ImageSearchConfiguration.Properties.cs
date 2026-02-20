namespace ImageSearchCL.API;

public static partial class ImageSearchConfiguration
{
    public static SynchronizationContext? EventSynchronizationContext
    {
        get => _eventSynchronizationContext;
        set => _eventSynchronizationContext = value;
    }

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

    public static bool EnableDebugOverlay
    {
        get => _enableDebugOverlay;
        set => _enableDebugOverlay = value;
    }

    public static System.Drawing.Color DebugOverlayColor
    {
        get => _debugOverlayColor;
        set => _debugOverlayColor = value;
    }

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

    public static IntPtr DebugOverlayWindowHandle
    {
        get => _debugOverlayWindowHandle;
        set => _debugOverlayWindowHandle = value;
    }

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
