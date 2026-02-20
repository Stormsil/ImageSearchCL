using System.Diagnostics;
using ImageSearchCL.API;
using ImageSearchCL.Infrastructure;

namespace ImageSearchCL.Core;

internal sealed partial class TrackingSession
{
    /// <summary>
    /// Handles incoming frames from the capture session.
    /// </summary>
    private void OnFrameReady(object? sender, FrameReadyEventArgs e)
    {
        try
        {
            _frameQueue.Enqueue(e.Frame);
        }
        catch (ObjectDisposedException)
        {
            e.Frame?.Dispose();
        }
    }

    /// <summary>
    /// Background processing loop that dequeues and processes frames.
    /// </summary>
    private async Task ProcessingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var frame = _frameQueue.Dequeue();
                if (frame == null)
                {
                    await Task.Delay(1, cancellationToken);
                    continue;
                }

                try
                {
                    TrackingState currentState;
                    lock (_stateLock)
                    {
                        currentState = _state;
                    }

                    if (currentState != TrackingState.Running)
                    {
                        frame.Dispose();
                        continue;
                    }

                    TemplateMatchingEngine.MatchResult? matchResult = null;
                    ReferenceImage? matchedTemplate = null;
                    int matchedTemplateIndex = -1;

                    for (int i = 0; i < _configuration.ReferenceImages.Length; i++)
                    {
                        var template = _configuration.ReferenceImages[i];
                        var result = TemplateMatchingEngine.FindTemplate(
                            frame,
                            template.Image!,
                            _configuration.ConfidenceThreshold);

                        if (result != null && (matchResult == null || result.Confidence > matchResult.Confidence))
                        {
                            matchResult = result;
                            matchedTemplate = template;
                            matchedTemplateIndex = i;
                        }
                    }

                    ProcessDetection(matchResult, matchedTemplate, matchedTemplateIndex, DateTime.UtcNow);
                }
                finally
                {
                    frame.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes a detection result and updates state/emits events.
    /// </summary>
    private void ProcessDetection(
        TemplateMatchingEngine.MatchResult? matchResult,
        ReferenceImage? matchedTemplate,
        int matchedTemplateIndex,
        DateTime timestamp)
    {
        lock (_stateLock)
        {
            if (matchResult == null)
            {
                HandleNotVisible();

                if (API.ImageSearchConfiguration.EnableDebugOverlay)
                {
                    try
                    {
                        DebugOverlay.Instance.Clear();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[TrackingSession] Error clearing debug overlay: {ex.Message}");
                    }
                }
            }
            else
            {
                var findResult = BuildFindResult(matchResult, matchedTemplate, matchedTemplateIndex, timestamp);
                HandleVisible(findResult);
            }
        }

        if (API.ImageSearchConfiguration.EnableDebugOverlay && matchResult != null)
        {
            try
            {
                var result = BuildFindResult(matchResult, matchedTemplate, matchedTemplateIndex, timestamp);
                DebugOverlay.Instance.RegisterDetection(
                    result,
                    API.ImageSearchConfiguration.DebugOverlayColor,
                    API.ImageSearchConfiguration.DebugOverlayThickness,
                    API.ImageSearchConfiguration.DebugOverlayWindowHandle);
            }
            catch
            {
                // Ignore overlay errors - don't disrupt tracking.
            }
        }
    }

    private FindResult BuildFindResult(
        TemplateMatchingEngine.MatchResult matchResult,
        ReferenceImage? matchedTemplate,
        int matchedTemplateIndex,
        DateTime timestamp)
    {
        if (_configuration.ReferenceImages.Length > 1 && matchedTemplate != null)
        {
            return new MultiFindResult(
                matchResult.X,
                matchResult.Y,
                matchResult.Width,
                matchResult.Height,
                matchResult.Confidence,
                timestamp,
                matchedTemplate,
                matchedTemplateIndex);
        }

        return new FindResult(
            matchResult.X,
            matchResult.Y,
            matchResult.Width,
            matchResult.Height,
            matchResult.Confidence,
            timestamp);
    }

}
