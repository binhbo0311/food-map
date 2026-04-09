using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;

namespace FOOD_MAP.Services;

public sealed class NarrationService : INarrationService
{
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private CancellationTokenSource? _currentPlaybackCts;
    private IReadOnlyList<Locale>? _cachedLocales;

    public Task PlayManualNarrationAsync(string fullDescription, string languageCode, CancellationToken cancellationToken = default)
    {
        // Luồng manual ưu tiên đọc đầy đủ nội dung cho người dùng.
        return SpeakTextAsync(fullDescription, languageCode, cancellationToken);
    }

    public Task PlayProximityNarrationAsync(string shortIntro, string languageCode, CancellationToken cancellationToken = default)
    {
        // Luồng proximity chỉ đọc lời giới thiệu ngắn để tránh làm phiền.
        return SpeakTextAsync(shortIntro, languageCode, cancellationToken);
    }

    public async Task EnqueuePoiNarrationAsync(int poiId, int languageId, CancellationToken cancellationToken = default)
    {
        // Phương án này sẽ được nâng cấp ở bước sau khi có repository lookup riêng cho narration.
        await SpeakTextAsync($"POI {poiId}", "vi", cancellationToken);
    }

    public async Task PlayAudioFileAsync(string localAudioFilePath, CancellationToken cancellationToken = default)
    {
        // Chưa xử lý play file âm thanh ở bước này, tạm thời fallback sang TTS nếu cần.
        await SpeakTextAsync(Path.GetFileNameWithoutExtension(localAudioFilePath), "vi", cancellationToken);
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await StopAsync(cancellationToken);

        await _playbackGate.WaitAsync(cancellationToken);
        try
        {
            _currentPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var playbackToken = _currentPlaybackCts.Token;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // TextToSpeech default dùng engine hệ thống, tránh overlap bằng cách hủy playback cũ.
                var speechOptions = new SpeechOptions
                {
                    Volume = 1.0f,
                    Pitch = 1.0f,
                    Locale = await ResolveLocaleAsync(languageCode)
                };

                await TextToSpeech.Default.SpeakAsync(text, speechOptions, playbackToken);
            });
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _currentPlaybackCts?.Cancel();
            _currentPlaybackCts?.Dispose();
        }
        finally
        {
            _currentPlaybackCts = null;
        }

        return Task.CompletedTask;
    }

    private async Task<Locale?> ResolveLocaleAsync(string languageCode)
    {
        try
        {
            if (_cachedLocales is null)
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                _cachedLocales = locales.ToList();
            }

            return _cachedLocales.FirstOrDefault(x => string.Equals(x.Language, languageCode, StringComparison.OrdinalIgnoreCase))
                   ?? _cachedLocales.FirstOrDefault(x => x.Language.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Nếu không lấy được locale thì để null để hệ thống tự chọn locale mặc định.
            return null;
        }
    }

}
