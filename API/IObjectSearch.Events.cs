namespace ImageSearchCL.API;

public partial interface IObjectSearch
{
    event EventHandler<FindResult> Appeared;

    event EventHandler<FindResult> Disappeared;

    event EventHandler<MovedEventArgs> Moved;

    event EventHandler<StateChangedEventArgs> StateChanged;
}
