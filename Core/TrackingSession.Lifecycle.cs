using System.Diagnostics;
using ImageSearchCL.API;
using ImageSearchCL.Infrastructure;

namespace ImageSearchCL.Core;

internal sealed partial class TrackingSession
{
    /// <inheritdoc/>
    public void Start()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.NotStarted)
                throw new InvalidOperationException($"Cannot start tracking session in state {_state}. Expected NotStarted.");

            ChangeState(TrackingState.Running);

            _processingCts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoop(_processingCts.Token), _processingCts.Token);

            _captureSession.Start();

            if (API.ImageSearchConfiguration.EnableDebugOverlay)
            {
                lock (_activeSessionLock)
                {
                    _activeSessionCount++;
                    if (_activeSessionCount == 1)
                    {
                        try
                        {
                            DebugOverlay.Instance.Show();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[TrackingSession] Error showing debug overlay: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot pause tracking session in state {_state}. Expected Running.");

            ChangeState(TrackingState.Paused);
        }
    }

    /// <inheritdoc/>
    public void Resume()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.Paused)
                throw new InvalidOperationException($"Cannot resume tracking session in state {_state}. Expected Paused.");

            ChangeState(TrackingState.Running);
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_stateLock)
        {
            if (_state == TrackingState.Stopped || _state == TrackingState.Disposed)
                return;

            ChangeState(TrackingState.Stopped);
            _captureSession.Stop();
            _processingCts?.Cancel();
        }

        try
        {
            _processingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        lock (_stateLock)
        {
            _processingCts?.Dispose();
            _processingCts = null;
            _processingTask = null;

            _frameQueue.Clear();

            if (API.ImageSearchConfiguration.EnableDebugOverlay)
            {
                lock (_activeSessionLock)
                {
                    _activeSessionCount--;
                    if (_activeSessionCount == 0)
                    {
                        try
                        {
                            DebugOverlay.Instance.Hide();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[TrackingSession] Error hiding debug overlay: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public FindResult? WaitUntilVisible(TimeSpan timeout)
    {
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackingSession));
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot wait in state {_state}. Session must be Running.");

            if (_visibility == ObjectVisibility.Visible && _lastResult != null)
                return _lastResult;
        }

        bool signaled = _visibleEvent.Wait(timeout);

        lock (_stateLock)
        {
            return signaled && _visibility == ObjectVisibility.Visible ? _lastResult : null;
        }
    }

    /// <inheritdoc/>
    public bool WaitUntilNotVisible(TimeSpan timeout)
    {
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackingSession));
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot wait in state {_state}. Session must be Running.");

            if (_visibility == ObjectVisibility.NotVisible)
                return true;
        }

        bool signaled = _notVisibleEvent.Wait(timeout);

        lock (_stateLock)
        {
            return signaled || _state != TrackingState.Running;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
                return;

            ChangeState(TrackingState.Disposed);

            _captureSession.FrameReady -= OnFrameReady;
            _captureSession.Dispose();
            _configuration.ReferenceImage.Dispose();
            _processingCts?.Cancel();

            _visibleEvent.Set();
            _notVisibleEvent.Set();
            _visibleEvent.Dispose();
            _notVisibleEvent.Dispose();

            _disposed = true;
        }

        try
        {
            _processingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _processingCts?.Dispose();
        _frameQueue.Dispose();

        if (API.ImageSearchConfiguration.EnableDebugOverlay)
        {
            lock (_activeSessionLock)
            {
                _activeSessionCount--;
                if (_activeSessionCount == 0)
                {
                    try
                    {
                        DebugOverlay.Instance.Hide();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
