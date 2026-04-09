namespace FOOD_MAP.Services;

public interface ISyncService
{
    // Đồng bộ metadata + nội dung cần thiết từ server về kho dữ liệu của hệ thống.
    Task SyncAllAsync(CancellationToken cancellationToken = default);

    // Kiểm tra nhanh app đã có dữ liệu local để chạy offline hay chưa.
    Task<bool> HasOfflineDataAsync(CancellationToken cancellationToken = default);
}
