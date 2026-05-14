namespace FOOD_MAP.Services;

using FOOD_MAP.Shared.Models;

public interface IDataService
{
    // Seed dữ liệu khởi tạo vào PostgreSQL để app có POI và ngôn ngữ ngay lần đầu chạy.
    Task SeedDataAsync(CancellationToken cancellationToken = default);

    // Lấy danh sách ngôn ngữ khả dụng từ database.
    Task<IReadOnlyList<Language>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default);
}
