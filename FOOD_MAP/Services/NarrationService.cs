using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;

namespace FOOD_MAP.Services;

public sealed class NarrationService : INarrationService
{
    // Gate đảm bảo chỉ có 1 item được phát tại một thời điểm.
    private readonly SemaphoreSlim _playbackGate = new(1, 1);

    // Lock bảo vệ truy cập vào _priorityQueue từ nhiều thread.
    private readonly object _queueLock = new();

    // Hàng đợi TTS theo priority: List được sort trước khi dequeue.
    // item ở index 0 có priority cao nhất (sẽ phát tiếp theo).
    private readonly List<PendingProximityItem> _priorityQueue = [];

    private CancellationTokenSource? _currentPlaybackCts;
    private bool _isProcessingQueue;
    private IReadOnlyList<Locale>? _cachedLocales;

    public Task PlayManualNarrationAsync(string fullDescription, string languageCode, CancellationToken cancellationToken = default)
    {
        // Luồng manual: xoá queue và cancel playback hiện tại, phát ngay lập tức toàn bộ nội dung.
        ClearQueue();
        return SpeakTextAsync(fullDescription, languageCode, cancellationToken);
    }

    public Task PlayProximityNarrationAsync(string shortIntro, string languageCode, CancellationToken cancellationToken = default)
    {
        // Luồng proximity đơn lẻ: wrap thành batch với priority mặc định.
        var item = new PendingProximityItem(string.Empty, 0, shortIntro, languageCode);
        return EnqueueProximityBatchAsync([item], cancellationToken);
    }

    /// <summary>
    /// Nhận batch nhiều POI từ geofence (user đứng trong bán kính nhiều POI cùng lúc).
    /// Sort theo Priority giảm dần (priority lớn hơn = quan trọng hơn = phát trước),
    /// rồi enqueue tuần tự để TTS phát từng cái một không bị chồng chéo.
    /// </summary>
    public Task EnqueueProximityBatchAsync(IReadOnlyList<PendingProximityItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Sort batch mới theo priority giảm dần trước khi thêm vào queue.
        var sorted = items.OrderByDescending(x => x.Priority).ToList();

        lock (_queueLock)
        {
            foreach (var item in sorted)
            {
                // Bỏ qua POI đã có trong queue để tránh duplicate.
                if (!string.IsNullOrWhiteSpace(item.PoiId)
                    && _priorityQueue.Any(q => string.Equals(q.PoiId, item.PoiId, StringComparison.Ordinal)))
                {
                    continue;
                }

                _priorityQueue.Add(item);
            }

            // Re-sort toàn bộ queue sau khi merge (trường hợp queue đang có item cũ hơn).
            _priorityQueue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        // Khởi động vòng lặp xử lý queue nếu chưa chạy.
        _ = ProcessQueueAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task EnqueuePoiNarrationAsync(string poiId, int languageId, CancellationToken cancellationToken = default)
    {
        // Phương án fallback: wrap vào batch với priority mặc định = 0.
        var item = new PendingProximityItem(poiId, 0, $"POI {poiId}", "vi");
        await EnqueueProximityBatchAsync([item], cancellationToken);
    }

    public async Task PlayAudioFileAsync(string localAudioFilePath, CancellationToken cancellationToken = default)
    {
        // Chưa xử lý play file âm thanh ở bước này, fallback sang TTS.
        await SpeakTextAsync(Path.GetFileNameWithoutExtension(localAudioFilePath), "vi", cancellationToken);
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await StopCurrentPlaybackAsync();

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
        ClearQueue();
        return StopCurrentPlaybackAsync();
    }

    // ── PRIVATE HELPERS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Vòng lặp xử lý queue: lấy item có priority cao nhất, phát, rồi lặp lại.
    /// Đảm bảo chỉ chạy 1 instance tại một thời điểm nhờ _isProcessingQueue flag.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        lock (_queueLock)
        {
            if (_isProcessingQueue)
            {
                return; // Đã có vòng lặp đang chạy, không cần mở thêm.
            }

            _isProcessingQueue = true;
        }

        try
        {
            while (true)
            {
                PendingProximityItem? next;
                lock (_queueLock)
                {
                    if (_priorityQueue.Count == 0)
                    {
                        _isProcessingQueue = false;
                        return;
                    }

                    // Dequeue item đầu tiên (priority cao nhất vì đã sort).
                    next = _priorityQueue[0];
                    _priorityQueue.RemoveAt(0);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    // Phát từng item tuần tự — không stop item đang phát nếu là
                    // proximity queue (khác với manual narration phải stop ngay).
                    await SpeakTextInternalAsync(next.NarrationText, next.LanguageCode, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Bỏ qua lỗi của một item để tiếp tục phát item tiếp theo.
                }
            }
        }
        finally
        {
            lock (_queueLock)
            {
                _isProcessingQueue = false;
            }
        }
    }

    /// <summary>
    /// Phát TTS mà không cancel playback đang chạy (dùng trong queue processor).
    /// </summary>
    private async Task SpeakTextInternalAsync(string text, string languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _playbackGate.WaitAsync(cancellationToken);
        try
        {
            _currentPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var playbackToken = _currentPlaybackCts.Token;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
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

    /// <summary>Chỉ dừng playback hiện tại, không xoá queue.</summary>
    private Task StopCurrentPlaybackAsync()
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

    /// <summary>Xoá toàn bộ hàng đợi TTS.</summary>
    private void ClearQueue()
    {
        lock (_queueLock)
        {
            _priorityQueue.Clear();
        }
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
