namespace ImageSearchCL.API;

/// <summary>
/// Represents the visibility state of a tracked object.
/// </summary>
public enum ObjectVisibility
{
    /// <summary>
    /// Object is not currently visible on screen (not detected or confidence below threshold).
    /// </summary>
    NotVisible,

    /// <summary>
    /// Object is currently visible on screen (detected with confidence above threshold).
    /// </summary>
    Visible
}
