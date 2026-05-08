namespace FOOD_MAP.Services;

public interface INarrationService
{
    // Luồng phát thủ công: đọc toàn bộ nội dung chi tiết cho người dùng.
    Task PlayManualNarrationAsync(string fullDescription, string languageCode, CancellationToken cancellationToken = default);

    // Luồng phát từ geofence: đọc câu giới thiệu ngắn gọn khi vào vùng.
    Task PlayProximityNarrationAsync(string shortIntro, string languageCode, CancellationToken cancellationToken = default);

    // Thêm một điểm POI vào hàng đợi phát nội dung theo priority.
    // priority cao hơn → xếp trước trong queue (số nhỏ hơn = ưu tiên cao hơn theo convention của app).
    Task EnqueuePoiNarrationAsync(string poiId, int languageId, CancellationToken cancellationToken = default);

    // Enqueue nhiều POI cùng lúc, sort theo priority giảm dần trước khi đưa vào queue.
    // Đây là API chính cho geofence khi user đứng trong bán kính của nhiều POI đồng thời.
    Task EnqueueProximityBatchAsync(IReadOnlyList<PendingProximityItem> items, CancellationToken cancellationToken = default);

    // Phát audio file local nếu tồn tại.
    Task PlayAudioFileAsync(string localAudioFilePath, CancellationToken cancellationToken = default);

    // Fallback qua TTS nếu không có audio file.
    Task SpeakTextAsync(string text, string languageCode, CancellationToken cancellationToken = default);

    // Dừng toàn bộ tiến trình phát hiện tại và xoá queue.
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Đại diện một POI đang chờ được phát TTS do geofence trigger.
/// Priority cao hơn (số lớn hơn) → phát trước.
/// </summary>
public sealed record PendingProximityItem(
    string PoiId,
    int Priority,
    string NarrationText,
    string LanguageCode);
