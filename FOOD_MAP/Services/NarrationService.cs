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

    public async Task EnqueuePoiNarrationAsync(string poiId, int languageId, CancellationToken cancellationToken = default)
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
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            if (_cachedLocales is null)
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                _cachedLocales = locales.ToList();
            }

            return _cachedLocales.FirstOrDefault(x => IsLocaleMatch(x, normalizedLanguageCode))
                   ?? _cachedLocales.FirstOrDefault(x =>
                       string.Equals(NormalizeLanguageCode(x.Language), normalizedLanguageCode, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Nếu không lấy được locale thì để null để hệ thống tự chọn locale mặc định.
            return null;
        }
    }

    private static bool IsLocaleMatch(Locale locale, string normalizedLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(locale.Language)
            && string.Equals(NormalizeLanguageCode(locale.Language), normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(locale.Name)
            && string.Equals(NormalizeLanguageCode(locale.Name), normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(locale.Country)
            && string.Equals(NormalizeLanguageCode(locale.Country), normalizedLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
        }

        var normalized = languageCode.Trim().ToLowerInvariant();

        if (normalized.StartsWith("vi", StringComparison.Ordinal)
            || normalized.Contains("viet", StringComparison.Ordinal)
            || normalized.Contains("vietnam", StringComparison.Ordinal))
        {
            return "vi";
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal)
            || normalized.Contains("english", StringComparison.Ordinal)
            || normalized.Contains("united states", StringComparison.Ordinal)
            || normalized.Contains("united kingdom", StringComparison.Ordinal))
        {
            return "en";
        }

        return normalized;
    }

}
