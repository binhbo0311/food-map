using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Utilities;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace FOOD_MAP.ViewModels;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private readonly IPoiRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly IDataService _dataService;
    private readonly IUserSessionService _userSessionService;
    private readonly IUserActivityRepository _userActivityRepository;
    private readonly List<PoiListItemViewModel> _allPoiItems = [];
    private CancellationTokenSource? _ttsTimerCts;
    private int _ttsPlaybackVersion;

    private bool _isPoiTabActive = true;
    private bool _isCameraRunning;
    private bool _isLoaded;
    private bool _isSeeded;
    private bool _isChangingLanguage;
    private bool _isBusyFavorite;
    private bool _isAuthenticatedUser;
    private bool _isTtsPlaying;
    private bool _isFoodMenuVisible;
    private string _searchQuery = string.Empty;
    private string _currentTtsTitle = "Chưa phát";
    private string _ttsElapsedText = "00:00";
    private string _ttsTotalText = "--:--";
    private string _foodMenuHeader = "Menu";
    private string _scannedQrPayload = string.Empty;
    private double _ttsProgress;

    private string _cameraStatusText = "Status: Off";
    private string _cameraButtonText = "Open Camera";
    private string _selectedPoiText = "Tap a POI to play audio.";
    private string _selectedLanguage = "vi";
    private string _sessionDisplayText = "Mode: Guest";

    public MainPageViewModel(
        IPoiRepository poiRepository,
        INarrationService narrationService,
        IDataService dataService,
        IUserSessionService userSessionService,
        IUserActivityRepository userActivityRepository)
    {
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _dataService = dataService;
        _userSessionService = userSessionService;
        _userActivityRepository = userActivityRepository;

        PoiItems = new ObservableCollection<PoiListItemViewModel>();
        FoodMenuItems = new ObservableCollection<FoodMenuItemViewModel>();
        AvailableLanguages = new ObservableCollection<string> { "vi", "en" };

        SelectPoiTabCommand = new Command(() => SetActiveTab(true));
        SelectCameraTabCommand = new Command(() => SetActiveTab(false));
        ToggleCameraCommand = new Command(ToggleCamera);
        PlayPoiCommand = new Command<PoiListItemViewModel>(async poi => await PlayPoiAsync(poi));
        ToggleFavoriteCommand = new Command<PoiListItemViewModel>(async poi => await ToggleFavoriteAsync(poi));
        StopTtsCommand = new Command(async () => await StopTtsAsync(), () => IsTtsPlaying);

        UpdateSessionStateLabel();
    }

    public ObservableCollection<PoiListItemViewModel> PoiItems { get; }

    public ObservableCollection<FoodMenuItemViewModel> FoodMenuItems { get; }

    public ObservableCollection<string> AvailableLanguages { get; }

    public ICommand SelectPoiTabCommand { get; }

    public ICommand SelectCameraTabCommand { get; }

    public ICommand ToggleCameraCommand { get; }

    public ICommand PlayPoiCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand StopTtsCommand { get; }

    public bool IsFoodMenuVisible
    {
        get => _isFoodMenuVisible;
        private set
        {
            if (_isFoodMenuVisible == value)
            {
                return;
            }

            _isFoodMenuVisible = value;
            OnPropertyChanged();
        }
    }

    public string FoodMenuHeader
    {
        get => _foodMenuHeader;
        private set
        {
            if (_foodMenuHeader == value)
            {
                return;
            }

            _foodMenuHeader = value;
            OnPropertyChanged();
        }
    }

    public string ScannedQrPayload
    {
        get => _scannedQrPayload;
        set
        {
            if (_scannedQrPayload == value)
            {
                return;
            }

            _scannedQrPayload = value;
            OnPropertyChanged();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value;
            OnPropertyChanged();
            ApplySearchFilter();
        }
    }

    public bool IsTtsPlaying
    {
        get => _isTtsPlaying;
        private set
        {
            if (_isTtsPlaying == value)
            {
                return;
            }

            _isTtsPlaying = value;
            OnPropertyChanged();
            (StopTtsCommand as Command)?.ChangeCanExecute();
        }
    }

    public string CurrentTtsTitle
    {
        get => _currentTtsTitle;
        private set
        {
            if (_currentTtsTitle == value)
            {
                return;
            }

            _currentTtsTitle = value;
            OnPropertyChanged();
        }
    }

    public string TtsElapsedText
    {
        get => _ttsElapsedText;
        private set
        {
            if (_ttsElapsedText == value)
            {
                return;
            }

            _ttsElapsedText = value;
            OnPropertyChanged();
        }
    }

    public string TtsTotalText
    {
        get => _ttsTotalText;
        private set
        {
            if (_ttsTotalText == value)
            {
                return;
            }

            _ttsTotalText = value;
            OnPropertyChanged();
        }
    }

    public double TtsProgress
    {
        get => _ttsProgress;
        private set
        {
            if (Math.Abs(_ttsProgress - value) < 0.001)
            {
                return;
            }

            _ttsProgress = value;
            OnPropertyChanged();
        }
    }

    public bool IsAuthenticatedUser
    {
        get => _isAuthenticatedUser;
        private set
        {
            if (_isAuthenticatedUser == value)
            {
                return;
            }

            _isAuthenticatedUser = value;
            OnPropertyChanged();
        }
    }

    public string SessionDisplayText
    {
        get => _sessionDisplayText;
        private set
        {
            if (_sessionDisplayText == value)
            {
                return;
            }

            _sessionDisplayText = value;
            OnPropertyChanged();
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            var normalizedLanguage = NormalizeLanguageCode(value);

            if (string.Equals(_selectedLanguage, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguage = normalizedLanguage;
            OnPropertyChanged();

            // Lưu ngôn ngữ đã chọn để geofence/TTS sử dụng cùng locale.
            Preferences.Default.Set("selected_language", normalizedLanguage);

            // Đổi ngôn ngữ sẽ tải lại danh sách POI và marker status liên quan.
            _ = ChangeLanguageAsync(normalizedLanguage);
        }
    }

    public bool IsPoiTabActive
    {
        get => _isPoiTabActive;
        private set
        {
            if (_isPoiTabActive == value)
            {
                return;
            }

            _isPoiTabActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCameraTabActive));
        }
    }

    public bool IsCameraTabActive => !IsPoiTabActive;

    public bool IsCameraRunning
    {
        get => _isCameraRunning;
        private set
        {
            if (_isCameraRunning == value)
            {
                return;
            }

            _isCameraRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CameraStatusText));
            OnPropertyChanged(nameof(CameraButtonText));
        }
    }

    public string CameraStatusText
    {
        get => _cameraStatusText;
        private set
        {
            if (_cameraStatusText == value)
            {
                return;
            }

            _cameraStatusText = value;
            OnPropertyChanged();
        }
    }

    public string CameraButtonText
    {
        get => _cameraButtonText;
        private set
        {
            if (_cameraButtonText == value)
            {
                return;
            }

            _cameraButtonText = value;
            OnPropertyChanged();
        }
    }

    public string SelectedPoiText
    {
        get => _selectedPoiText;
        private set
        {
            if (_selectedPoiText == value)
            {
                return;
            }

            _selectedPoiText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadPoisAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        if (!_isSeeded)
        {
            // Seed dữ liệu khởi tạo vào PostgreSQL nếu database mới.
            await _dataService.SeedDataAsync(cancellationToken);
            _isSeeded = true;
        }

        UpdateSessionStateLabel();

        var selectedLanguageCode = NormalizeLanguageCode(SelectedLanguage);
        var poiItems = await _poiRepository.GetPoiItemsAsync(selectedLanguageCode, cancellationToken);

        HashSet<string> favoritePoiIds = [];
        HashSet<string> visitedPoiIds = [];
        var totalTours = 0;
        var currentUserId = _userSessionService.CurrentUserId;
        if (IsAuthenticatedUser && currentUserId.HasValue)
        {
            // Nếu trước đó có thao tác offline thì thử đẩy hàng đợi lên PostgreSQL trước khi nạp trạng thái mới nhất.
            await _userActivityRepository.FlushPendingOperationsAsync(cancellationToken);

            // Sau đăng nhập, tải dữ liệu cá nhân để khôi phục trạng thái favorites và lịch sử tour.
            favoritePoiIds = await _userActivityRepository.GetFavoritePoiIdsAsync(currentUserId.Value, cancellationToken);
            visitedPoiIds = await _userActivityRepository.GetVisitedPoiIdsAsync(currentUserId.Value, cancellationToken);
            totalTours = await _userActivityRepository.GetTourCountAsync(currentUserId.Value, cancellationToken);
        }

        _allPoiItems.Clear();
        foreach (var poiItem in poiItems)
        {
            poiItem.IsFavorite = favoritePoiIds.Contains(poiItem.PoiId);
            poiItem.IsVisited = visitedPoiIds.Contains(poiItem.PoiId);
            _allPoiItems.Add(poiItem);
        }

        ApplySearchFilter();

        if (PoiItems.Count == 0)
        {
            SelectedPoiText = "No POI found in PostgreSQL.";
        }
        else
        {
            SelectedPoiText = $"Loaded {PoiItems.Count} POI items from PostgreSQL.";
        }

        if (IsAuthenticatedUser && currentUserId.HasValue)
        {
            SessionDisplayText = $"Mode: User ({_userSessionService.DisplayName}) | Favorites: {favoritePoiIds.Count} | Tours: {totalTours}";

            // Hiển thị kết quả đồng bộ ngay sau khi đăng nhập để user biết dữ liệu đã được tải từ PostgreSQL.
            SelectedPoiText = $"Đã đồng bộ từ PostgreSQL: {favoritePoiIds.Count} favorites, {totalTours} tours.";
        }

        _isLoaded = true;
    }

    public void InvalidateData()
    {
        _isLoaded = false;
    }

    private async Task ChangeLanguageAsync(string? languageCode)
    {
        if (_isChangingLanguage)
        {
            return;
        }

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            return;
        }

        if (!AvailableLanguages.Any(x => string.Equals(x, normalizedLanguageCode, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _isChangingLanguage = true;
        try
        {
            _selectedLanguage = normalizedLanguageCode;
            _isLoaded = false;
            await LoadPoisAsync();
        }
        finally
        {
            _isChangingLanguage = false;
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();

        // Chuẩn hóa các biến thể nhập liệu để luôn map đúng mã ngôn ngữ hệ thống.
        if (normalized.StartsWith("en", StringComparison.Ordinal) || normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        if (normalized.StartsWith("vi", StringComparison.Ordinal) || normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "vi";
        }

        return normalized;
    }

    public void SetStatusMessage(string message)
    {
        SelectedPoiText = message;
    }

    private void SetActiveTab(bool isPoiTab)
    {
        IsPoiTabActive = isPoiTab;

        if (isPoiTab)
        {
            StopCamera();
        }
    }

    private void ToggleCamera()
    {
        if (!IsCameraTabActive)
        {
            return;
        }

        if (IsCameraRunning)
        {
            StopCamera();
            return;
        }

        StartCamera();
    }

    private void StartCamera()
    {
        IsCameraRunning = true;
        CameraStatusText = "Status: On";
        CameraButtonText = "Close Camera";
    }

    private void StopCamera()
    {
        IsCameraRunning = false;
        CameraStatusText = "Status: Off";
        CameraButtonText = "Open Camera";
    }

    public async Task OnPoiSelectedAsync(PoiListItemViewModel? poi)
    {
        if (poi is null)
        {
            HideFoodMenu();
            return;
        }

        await UpdateFoodMenuAsync(poi);
    }

    public async Task<IReadOnlyList<PoiAvailableLanguageOption>> GetAvailableLanguagesForQrAsync(string scannedPoiId, CancellationToken cancellationToken = default)
    {
        var options = await _poiRepository.GetAvailableLanguagesForPoiAsync(scannedPoiId, cancellationToken);

        foreach (var option in options)
        {
            if (AvailableLanguages.Any(x => string.Equals(x, option.LanguageCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            AvailableLanguages.Add(option.LanguageCode);
        }

        return options;
    }

    public static string? ExtractPoiIdFromQrPayload(string? qrPayload)
    {
        return QrPayloadParser.ExtractPoiIdFromPayload(qrPayload);
    }

    public async Task<PoiScanResult?> HandleQrScanAsync(string scannedPoiId, string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = ExtractPoiIdFromQrPayload(scannedPoiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            SelectedPoiText = "QR payload does not contain a valid POI id.";
            return null;
        }

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        if (!AvailableLanguages.Any(x => string.Equals(x, normalizedLanguageCode, StringComparison.OrdinalIgnoreCase)))
        {
            AvailableLanguages.Add(normalizedLanguageCode);
        }

        var languageChanged = !string.Equals(_selectedLanguage, normalizedLanguageCode, StringComparison.OrdinalIgnoreCase);
        if (languageChanged)
        {
            _selectedLanguage = normalizedLanguageCode;
            OnPropertyChanged(nameof(SelectedLanguage));
            Preferences.Default.Set("selected_language", normalizedLanguageCode);

            // QR điều hướng ngôn ngữ sẽ đồng bộ lại danh sách POI theo locale mới.
            _isLoaded = false;
            await LoadPoisAsync(cancellationToken);
        }

        var scanResult = await _poiRepository.GetPoiScanResultAsync(normalizedPoiId, normalizedLanguageCode, cancellationToken);
        if (scanResult is null)
        {
            SelectedPoiText = $"QR POI '{normalizedPoiId}' not found or not approved.";
            HideFoodMenu();
            return null;
        }

        SelectedPoiText = $"QR matched: {scanResult.LocationName} ({normalizedLanguageCode}).";

        if (scanResult.PoiType == PoiType.Food)
        {
            ApplyFoodMenuItems(scanResult.LocationName, scanResult.FoodItems);
        }
        else
        {
            HideFoodMenu();
        }

        await _narrationService.StopAsync();
        await _narrationService.PlayManualNarrationAsync(scanResult.TtsScript, normalizedLanguageCode, cancellationToken);
        _ = _poiRepository.RecordListenAsync(normalizedPoiId);
        return scanResult;
    }

    public async Task StopTtsAsyncForCamera(CancellationToken cancellationToken = default)
    {
        await _narrationService.StopAsync();
    }

    private async Task PlayPoiAsync(PoiListItemViewModel? poi)
    {
        if (poi is null)
        {
            return;
        }

        SelectedPoiText = $"Selected: {poi.Name}";
        await UpdateFoodMenuAsync(poi);
        _ = SpeakPoiAsync(poi);
    }

    private async Task UpdateFoodMenuAsync(PoiListItemViewModel poi)
    {
        if (!poi.IsFoodPoi)
        {
            HideFoodMenu();
            return;
        }

        var items = await _poiRepository.GetFoodItemsByPoiIdAsync(poi.PoiId);
        ApplyFoodMenuItems(poi.Name, items);
    }

    private void ApplyFoodMenuItems(string poiName, IReadOnlyList<FoodMenuItemViewModel> items)
    {
        FoodMenuItems.Clear();
        foreach (var item in items)
        {
            FoodMenuItems.Add(item);
        }

        FoodMenuHeader = string.IsNullOrWhiteSpace(poiName)
            ? "Food Menu"
            : $"Food Menu - {poiName}";

        IsFoodMenuVisible = FoodMenuItems.Count > 0;
    }

    private void HideFoodMenu()
    {
        FoodMenuItems.Clear();
        FoodMenuHeader = "Food Menu";
        IsFoodMenuVisible = false;
    }

    private async Task SpeakPoiAsync(PoiListItemViewModel poi)
    {
        var fullDescription = string.IsNullOrWhiteSpace(poi.Description) ? poi.NarrationText : poi.Description;
        var estimatedTotalSeconds = Math.Max(5, (int)Math.Ceiling(fullDescription.Length / 11.0));
        var playbackVersion = Interlocked.Increment(ref _ttsPlaybackVersion);
        var narrationSucceeded = false;

        try
        {
            await _narrationService.StopAsync();

            StartTtsPlaybackUi(playbackVersion, poi.Name, estimatedTotalSeconds);

            // Luồng manual: đọc đầy đủ Description theo yêu cầu.
            await _narrationService.PlayManualNarrationAsync(fullDescription, SelectedLanguage);
            _ = _poiRepository.RecordListenAsync(poi.PoiId);
            narrationSucceeded = true;
        }
        catch
        {
            // Không cho TTS/database làm app crash nếu service bị lỗi.
            SelectedPoiText = "Unable to play narration right now.";
        }
        finally
        {
            StopTtsPlaybackUi(playbackVersion, resetToIdle: true);
        }

        if (!narrationSucceeded)
        {
            return;
        }

        // User mode mới lưu lịch sử tour để phục vụ đồng bộ lại lần đăng nhập sau.
        var currentUserId = _userSessionService.CurrentUserId;
        if (!IsAuthenticatedUser || !currentUserId.HasValue)
        {
            return;
        }

        try
        {
            await _userActivityRepository.AddTourAsync(currentUserId.Value, poi.PoiId, SelectedLanguage, "manual");

            // Đánh dấu POI đã ghé ngay sau khi đồng bộ lịch sử tour thành công.
            poi.IsVisited = true;
            SelectedPoiText = $"Saved tour activity: {poi.Name} (auto-sync if offline).";
        }
        catch
        {
            SelectedPoiText = "Unable to save tour activity right now.";
        }
    }

    private async Task StopTtsAsync()
    {
        var playbackVersion = Interlocked.Increment(ref _ttsPlaybackVersion);

        try
        {
            await _narrationService.StopAsync();
            SelectedPoiText = "Đã dừng phát TTS.";
        }
        catch
        {
            SelectedPoiText = "Không thể dừng TTS lúc này.";
        }
        finally
        {
            StopTtsPlaybackUi(playbackVersion, resetToIdle: true);
        }
    }

    private void ApplySearchFilter()
    {
        var query = SearchQuery.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allPoiItems.ToList()
            : _allPoiItems
                .Where(x =>
                    x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || x.DistanceText.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        PoiItems.Clear();
        foreach (var poi in filtered)
        {
            PoiItems.Add(poi);
        }

        if (PoiItems.Count == 0)
        {
            SelectedPoiText = string.IsNullOrWhiteSpace(query)
                ? "Không có POI trong PostgreSQL."
                : $"Không tìm thấy POI với từ khóa: {query}";
            return;
        }

        // Đảm bảo chỉ có một POI được đánh dấu nổi bật trong danh sách hiện tại.
        var nearest = PoiItems.FirstOrDefault(x => x.IsNearest) ?? PoiItems[0];
        foreach (var item in PoiItems)
        {
            item.IsNearest = item == nearest;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            SelectedPoiText = $"Tìm thấy {PoiItems.Count} POI với từ khóa '{query}'.";
        }
    }

    private void StartTtsPlaybackUi(int playbackVersion, string poiName, int totalSeconds)
    {
        if (playbackVersion != _ttsPlaybackVersion)
        {
            return;
        }

        _ttsTimerCts?.Cancel();
        _ttsTimerCts?.Dispose();
        _ttsTimerCts = new CancellationTokenSource();

        CurrentTtsTitle = $"Đang phát: {poiName}";
        TtsElapsedText = "00:00";
        TtsTotalText = FormatDuration(TimeSpan.FromSeconds(totalSeconds));
        TtsProgress = 0;
        IsTtsPlaying = true;

        _ = RunTtsClockAsync(playbackVersion, DateTimeOffset.UtcNow, totalSeconds, _ttsTimerCts.Token);
    }

    private void StopTtsPlaybackUi(int playbackVersion, bool resetToIdle)
    {
        if (playbackVersion != _ttsPlaybackVersion)
        {
            return;
        }

        _ttsTimerCts?.Cancel();
        _ttsTimerCts?.Dispose();
        _ttsTimerCts = null;

        IsTtsPlaying = false;
        TtsProgress = 0;
        TtsElapsedText = "00:00";

        if (resetToIdle)
        {
            CurrentTtsTitle = "Chưa phát";
            TtsTotalText = "--:--";
        }
    }

    private async Task RunTtsClockAsync(int playbackVersion, DateTimeOffset startedUtc, int totalSeconds, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && playbackVersion == _ttsPlaybackVersion)
        {
            var elapsed = DateTimeOffset.UtcNow - startedUtc;
            var clampedElapsedSeconds = Math.Min(totalSeconds, Math.Max(0, (int)Math.Round(elapsed.TotalSeconds)));
            var progress = totalSeconds <= 0 ? 0 : (double)clampedElapsedSeconds / totalSeconds;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TtsElapsedText = FormatDuration(TimeSpan.FromSeconds(clampedElapsedSeconds));
                TtsProgress = progress;
            });

            if (clampedElapsedSeconds >= totalSeconds)
            {
                break;
            }

            try
            {
                await Task.Delay(250, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
    }

    private async Task ToggleFavoriteAsync(PoiListItemViewModel? poi)
    {
        if (poi is null || _isBusyFavorite)
        {
            return;
        }

        var currentUserId = _userSessionService.CurrentUserId;
        if (!IsAuthenticatedUser || !currentUserId.HasValue)
        {
            SelectedPoiText = "Guest mode: Login to save favorites.";
            return;
        }

        _isBusyFavorite = true;
        try
        {
            var targetFavoriteState = !poi.IsFavorite;
            var isFavoriteNow = await _userActivityRepository.SetFavoriteAsync(currentUserId.Value, poi.PoiId, targetFavoriteState);
            poi.IsFavorite = isFavoriteNow;
            SelectedPoiText = isFavoriteNow
                ? $"Saved favorite: {poi.Name} (auto-sync if offline)."
                : $"Saved unfavorite: {poi.Name} (auto-sync if offline).";
        }
        catch
        {
            SelectedPoiText = "Unable to save favorite right now.";
        }
        finally
        {
            _isBusyFavorite = false;
        }
    }

    private void UpdateSessionStateLabel()
    {
        IsAuthenticatedUser = !_userSessionService.IsGuest && _userSessionService.CurrentUserId.HasValue;

        SessionDisplayText = IsAuthenticatedUser
            ? $"Mode: User ({_userSessionService.DisplayName})"
            : "Mode: Guest";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
