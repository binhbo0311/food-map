namespace FOOD_MAP.Services;

public interface IDataService
{
    // Seed dữ liệu khởi tạo vào PostgreSQL để app có POI và ngôn ngữ ngay lần đầu chạy.
    Task SeedDataAsync(CancellationToken cancellationToken = default);
}
