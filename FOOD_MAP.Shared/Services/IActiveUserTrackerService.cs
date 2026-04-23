namespace FOOD_MAP.Shared.Services;

public sealed record ActiveUserSummary(int TotalActiveClients, int ActiveWebClients, int ActiveMobileClients, int ActiveAuthenticatedUsers);

public interface IActiveUserTrackerService
{
    Task TrackWebHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default);

    Task TrackMobileHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default);

    Task<ActiveUserSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
