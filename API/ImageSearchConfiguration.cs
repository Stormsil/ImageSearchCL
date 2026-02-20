namespace ImageSearchCL.API;

/// <summary>
/// Global configuration settings for ImageSearchCL library.
/// </summary>
public static partial class ImageSearchConfiguration
{
    private static SynchronizationContext? _eventSynchronizationContext;
    private static TimeSpan _defaultWaitTimeout = TimeSpan.FromSeconds(30);
    private static TemplateMatchMode _matchMode = TemplateMatchMode.CCoeffNormed;
    private static bool _enableDebugOverlay;
    private static System.Drawing.Color _debugOverlayColor = System.Drawing.Color.Lime;
    private static int _debugOverlayThickness = 2;
    private static IntPtr _debugOverlayWindowHandle = IntPtr.Zero;
    private static double _defaultConfidence = 0.8;
    private static double _defaultMovementThreshold = 5.0;
    private static double _defaultOverlapThreshold = 0.5;
}
