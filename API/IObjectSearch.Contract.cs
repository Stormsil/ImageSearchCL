namespace ImageSearchCL.API;

public partial interface IObjectSearch
{
    TrackingState State { get; }

    FindResult? LastResult { get; }

    TrackingConfiguration Configuration { get; }

    void Start();

    void Pause();

    void Resume();

    void Stop();

    FindResult? WaitUntilVisible(TimeSpan timeout);

    bool WaitUntilNotVisible(TimeSpan timeout);
}
