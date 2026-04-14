namespace FOOD_MAP.Services;

public interface IUserActivityRepository
{
    Task<HashSet<string>> GetFavoritePoiIdsAsync(int userId, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetVisitedPoiIdsAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetTourCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> SetFavoriteAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken = default);

    Task AddTourAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken = default);

    Task<int> FlushPendingOperationsAsync(CancellationToken cancellationToken = default);
}
