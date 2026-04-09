namespace FOOD_MAP.Services;

public interface INarrationService
{
    // Luồng phát thủ công: đọc toàn bộ nội dung chi tiết cho người dùng.
    Task PlayManualNarrationAsync(string fullDescription, string languageCode, CancellationToken cancellationToken = default);

    // Luồng phát từ geofence: đọc câu giới thiệu ngắn gọn khi vào vùng.
    Task PlayProximityNarrationAsync(string shortIntro, string languageCode, CancellationToken cancellationToken = default);

    // Thêm một điểm POI vào hàng đợi phát nội dung.
    Task EnqueuePoiNarrationAsync(int poiId, int languageId, CancellationToken cancellationToken = default);

    // Phát audio file local nếu tồn tại.
    Task PlayAudioFileAsync(string localAudioFilePath, CancellationToken cancellationToken = default);

    // Fallback qua TTS nếu không có audio file.
    Task SpeakTextAsync(string text, string languageCode, CancellationToken cancellationToken = default);

    // Dừng toàn bộ tiến trình phát hiện tại.
    Task StopAsync(CancellationToken cancellationToken = default);
}
