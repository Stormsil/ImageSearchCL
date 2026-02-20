using ImageSearchCL.API;

namespace ImageSearchCL.Core;

internal sealed partial class TrackingSession
{
    /// <summary>
    /// Handles the case where object is not visible (below confidence threshold).
    /// </summary>
    private void HandleNotVisible()
    {
        if (_visibility == ObjectVisibility.Visible)
        {
            var lastResult = _lastResult!;
            _visibility = ObjectVisibility.NotVisible;
            _lastResult = null;

            _visibleEvent.Reset();
            _notVisibleEvent.Set();

            RaiseEvent(Disappeared, lastResult);
        }
    }

    /// <summary>
    /// Handles the case where object is visible (above confidence threshold).
    /// </summary>
    private void HandleVisible(FindResult newResult)
    {
        if (_visibility == ObjectVisibility.NotVisible)
        {
            _visibility = ObjectVisibility.Visible;
            _lastResult = newResult;

            _notVisibleEvent.Reset();
            _visibleEvent.Set();

            RaiseEvent(Appeared, newResult);
            return;
        }

        var oldResult = _lastResult!;
        double distance = oldResult.DistanceTo(newResult);

        if (distance >= _configuration.MovementThreshold)
        {
            _lastResult = newResult;
            var movedArgs = new MovedEventArgs(oldResult, newResult);
            RaiseEvent(Moved, movedArgs);
        }
        else
        {
            _lastResult = newResult;
        }
    }

    /// <summary>
    /// Changes the tracking state and raises StateChanged event.
    /// </summary>
    /// <remarks>
    /// Must be called under _stateLock.
    /// </remarks>
    private void ChangeState(TrackingState newState)
    {
        var oldState = _state;
        _state = newState;

        var args = new StateChangedEventArgs(oldState, newState, DateTime.UtcNow);
        RaiseEvent(StateChanged, args);
    }

    /// <summary>
    /// Raises an event on the SynchronizationContext (UI-safe).
    /// </summary>
    private void RaiseEvent<T>(EventHandler<T>? eventHandler, T args)
    {
        if (eventHandler == null)
            return;

        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => eventHandler.Invoke(this, args), null);
            return;
        }

        eventHandler.Invoke(this, args);
    }
}
