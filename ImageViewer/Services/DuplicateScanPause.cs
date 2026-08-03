using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public sealed class DuplicateScanPause
{
    private readonly object _gate = new();
    private TaskCompletionSource? _resumeSource;

    public bool IsPaused
    {
        get
        {
            lock (_gate) return _resumeSource is not null;
        }
    }

    public void Pause()
    {
        lock (_gate)
            _resumeSource ??= new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        TaskCompletionSource? source;
        lock (_gate)
        {
            source = _resumeSource;
            _resumeSource = null;
        }
        source?.TrySetResult();
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask;
        lock (_gate) waitTask = _resumeSource?.Task;
        if (waitTask is not null)
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
