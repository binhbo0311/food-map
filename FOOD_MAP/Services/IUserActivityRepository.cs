namespace FOOD_MAP.Services;

public interface IUserActivityRepository
{
    Task<HashSet<int>> GetFavoritePoiIdsAsync(int userId, CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetVisitedPoiIdsAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> GetTourCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> SetFavoriteAsync(int userId, int poiId, bool isFavorite, CancellationToken cancellationToken = default);

    Task AddTourAsync(int userId, int poiId, string languageCode, string triggerType, CancellationToken cancellationToken = default);

    Task<int> FlushPendingOperationsAsync(CancellationToken cancellationToken = default);
}
