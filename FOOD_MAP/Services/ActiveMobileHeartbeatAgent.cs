namespace FOOD_MAP.Services;

using FOOD_MAP.Shared.Services;

public interface IActiveMobileHeartbeatAgent
{
    void Start();

    void Stop();
}

public sealed class ActiveMobileHeartbeatAgent : IActiveMobileHeartbeatAgent
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);

    private readonly IActiveUserTrackerService _activeUserTrackerService;
    private readonly IUserSessionService _userSessionService;
    private readonly string _sessionKey = $"mobile-{Guid.NewGuid():N}";

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public ActiveMobileHeartbeatAgent(
        IActiveUserTrackerService activeUserTrackerService,
        IUserSessionService userSessionService)
    {
        _activeUserTrackerService = activeUserTrackerService;
        _userSessionService = userSessionService;
    }

    public void Start()
    {
        if (_loopCts is not null)
        {
            return;
        }

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunHeartbeatLoopAsync(_loopCts.Token));
    }

    public void Stop()
    {
        if (_loopCts is null)
        {
            return;
        }

        _loopCts.Cancel();
        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendHeartbeatAsync(cancellationToken);

            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SendHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _activeUserTrackerService.TrackMobileHeartbeatAsync(_userSessionService.CurrentUserId, _sessionKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Bỏ qua lỗi tạm thời để trải nghiệm app không bị gián đoạn.
        }
    }
}
