using FOOD_MAP.Services;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using AppModel = Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace FOOD_MAP
{
    public partial class MainPage : ContentPage
    {
        private const double CollapsedBottomRatio = 0.25;
        private const double ExpandedBottomRatio = 0.75;
        private const double TourArrivalRadiusMeters = 20;
        private static readonly TimeSpan MarkerDoubleTapWindow = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan RouteArrivalPollInterval = TimeSpan.FromSeconds(4);
        private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex ScriptTagRegex = new("<script\\b[^<]*(?:(?!</script>)<[^<]*)*</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex StyleTagRegex = new("<style\\b[^<]*(?:(?!</style>)<[^<]*)*</style>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex OnEventAttributeRegex = new("\\son[a-z]+\\s*=\\s*(\"[^\"]*\"|'[^']*')", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JavascriptHrefRegex = new("(href|src)\\s*=\\s*(\"|')\\s*javascript:[^\"']*(\"|')", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly MainPageViewModel _viewModel;
        private readonly ILocationService _locationService;
        private readonly IUserSessionService _userSessionService;
        private readonly IUserActivityRepository _userActivityRepository;
        private readonly HashSet<string> _routeArrivalLatchByPoiId = new(StringComparer.OrdinalIgnoreCase);
        private PoiListItemViewModel? _selectedPoi;
        private Location? _activeRouteDestination;
        private string? _activeRoutePoiId;
        private string? _activeRoutePoiName;
        private string? _lastMarkerTapPoiId;
        private DateTimeOffset _lastMarkerTapUtc;
        private CancellationTokenSource? _routeArrivalMonitorCts;
        private bool _isRouteArrivalSavedForCurrentSession;
        private bool _isSavingRouteArrival;
        private double _bottomSheetRatio = CollapsedBottomRatio;
        private bool _isAnimatingSheet;
    #if ANDROID
        private bool _isLocationTrackingStarted;
    #endif

        public MainPage()
        {
            InitializeComponent();

            // Dang ky cu chi vuot de de dang doi trang thai bottom sheet.
            var swipeUpGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Up };
            swipeUpGesture.Swiped += (s, e) => OnBottomSheetSwipedUp();
            PoiBottomSheet.GestureRecognizers.Add(swipeUpGesture);

            var swipeDownGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Down };
            swipeDownGesture.Swiped += (s, e) => OnBottomSheetSwipedDown();
            PoiBottomSheet.GestureRecognizers.Add(swipeDownGesture);

            var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
            var poiRepository = services.GetRequiredService<IPoiRepository>();
            var narrationService = services.GetRequiredService<INarrationService>();
            var dataService = services.GetRequiredService<IDataService>();
            var userSessionService = services.GetRequiredService<IUserSessionService>();
            var userActivityRepository = services.GetRequiredService<IUserActivityRepository>();
            _locationService = services.GetRequiredService<ILocationService>();
            _userSessionService = userSessionService;
            _userActivityRepository = userActivityRepository;
            _viewModel = new MainPageViewModel(poiRepository, narrationService, dataService, userSessionService, userActivityRepository);
            BindingContext = _viewModel;

            _viewModel.PoiItems.CollectionChanged += (_, _) => RenderPoiPins();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                ApplyBottomSheetRatio(_bottomSheetRatio);
                await _viewModel.LoadPoisAsync();
                RenderPoiPins();

#if ANDROID
                await EnsureLocationTrackingAsync();
#endif
            }
            catch (Exception ex)
            {
                // Hien thi loi thuc te de de debug thay vi bi boc thanh JavaProxyThrowable.
                _viewModel.SetStatusMessage($"Load POI failed: {ex.Message}");
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            ApplyBottomSheetRatio(_bottomSheetRatio);
        }

        private async void OnPoiTabClicked(object? sender, EventArgs e)
        {
            await ExpandBottomSheetAsync();
        }

        private async void OnCameraTabClicked(object? sender, EventArgs e)
        {
            await ExpandBottomSheetAsync();
        }

        private async void OnPoiSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not PoiListItemViewModel selectedPoi)
            {
                return;
            }

            SetSelectedPoi(selectedPoi);
            await _viewModel.OnPoiSelectedAsync(selectedPoi);

            // Tu dong dua camera ban do den POI vua chon de nguoi dung khong can tu keo tay.
            MoveMapToPoi(selectedPoi, 400);

            // Cuon item duoc chon len vi tri giua man hinh de nguoi dung nhin ro hon.
            PoiCollectionView.ScrollTo(selectedPoi, position: ScrollToPosition.Center, animate: true);

            // Bo chon de lan cham tiep theo van kich hoat scroll lai.
            PoiCollectionView.SelectedItem = null;
        }

        private async void OnProcessQrClicked(object? sender, EventArgs e)
        {
            var rawQrPayload = QrPayloadEntry.Text;
            var scannedPoiId = MainPageViewModel.ExtractPoiIdFromQrPayload(rawQrPayload);
            if (string.IsNullOrWhiteSpace(scannedPoiId))
            {
                await DisplayAlertAsync("QR", "QR payload is invalid. Please scan a valid POI id (example: VS-002).", "OK");
                return;
            }

            QrPayloadEntry.Text = scannedPoiId;

            var languageOptions = await _viewModel.GetAvailableLanguagesForQrAsync(scannedPoiId);
            if (languageOptions.Count == 0)
            {
                await DisplayAlertAsync("QR", $"No translation language is available in database for POI '{scannedPoiId}'.", "OK");
                return;
            }

            var optionLabels = languageOptions.Select(x => x.PromptLabel).ToArray();
            var selectedLabel = await DisplayActionSheetAsync(
                "Which language group do you belong to?",
                "Cancel",
                null,
                optionLabels);

            if (string.IsNullOrWhiteSpace(selectedLabel) || string.Equals(selectedLabel, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var selectedLanguage = languageOptions.FirstOrDefault(x => string.Equals(x.PromptLabel, selectedLabel, StringComparison.Ordinal));
            if (selectedLanguage is null)
            {
                await DisplayAlertAsync("QR", "Selected language is invalid.", "OK");
                return;
            }

            var scanResult = await _viewModel.HandleQrScanAsync(scannedPoiId, selectedLanguage.LanguageCode);
            if (scanResult is null)
            {
                return;
            }

            var scannedPoiItem = _viewModel.PoiItems.FirstOrDefault(x => string.Equals(x.PoiId, scanResult.PoiId, StringComparison.OrdinalIgnoreCase));
            if (scannedPoiItem is null)
            {
                return;
            }

            SetSelectedPoi(scannedPoiItem);
            await _viewModel.OnPoiSelectedAsync(scannedPoiItem);
            MoveMapToPoi(scannedPoiItem, 320);
            PoiCollectionView.ScrollTo(scannedPoiItem, position: ScrollToPosition.Center, animate: true);
        }

        private void MoveMapToPoi(PoiListItemViewModel poi, double radiusInMeters)
        {
            map.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(poi.Latitude, poi.Longitude),
                Distance.FromMeters(radiusInMeters)));
        }

        private void RenderPoiPins()
        {
            map.Pins.Clear();

            if (_viewModel.PoiItems.Count == 0)
            {
                _selectedPoi = null;
                HideSelectedPoiCard();
                return;
            }

            var selectedPoiInList = _selectedPoi is null
                ? null
                : _viewModel.PoiItems.FirstOrDefault(x => x.PoiId == _selectedPoi.PoiId);

            if (_selectedPoi is not null && selectedPoiInList is null)
            {
                _selectedPoi = null;
                HideSelectedPoiCard();
            }

            foreach (var poi in _viewModel.PoiItems)
            {
                var isSelectedPoi = selectedPoiInList is not null && poi.PoiId == selectedPoiInList.PoiId;
                var pin = new Pin
                {
                    Type = isSelectedPoi ? PinType.SavedPin : PinType.Place,
                    Label = isSelectedPoi ? $"★ {poi.Name}" : poi.Name,
                    Address = poi.DistanceText,
                    Location = new Location(poi.Latitude, poi.Longitude)
                };

                pin.BindingContext = poi;
                pin.MarkerClicked += OnPoiPinMarkerClicked;

                map.Pins.Add(pin);
            }

            var focusPoi = selectedPoiInList ?? (_viewModel.PoiItems.FirstOrDefault(x => x.IsNearest) ?? _viewModel.PoiItems[0]);
            var focusRadius = selectedPoiInList is null ? 600 : 400;
            MoveMapToPoi(focusPoi, focusRadius);
        }

        private void SetSelectedPoi(PoiListItemViewModel poi)
        {
            _selectedPoi = poi;

            SelectedPoiNameLabel.Text = poi.Name;
            SelectedPoiDistanceLabel.Text = poi.DistanceText;
            SelectedPoiDescriptionLabel.Text = poi.Description;
            ApplySelectedPoiImage(poi.ImageUrl);
            ApplySelectedPoiRichContent(poi.RichContentHtml);

            if (!SelectedPoiOverlay.IsVisible)
            {
                SelectedPoiOverlay.Opacity = 0;
                SelectedPoiOverlay.IsVisible = true;
                _ = SelectedPoiOverlay.FadeToAsync(1, 180, Easing.CubicOut);
            }

            RenderPoiPins();
        }

        private async void OnPoiPinMarkerClicked(object? sender, PinClickedEventArgs e)
        {
            if (sender is not Pin { BindingContext: PoiListItemViewModel poi })
            {
                return;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var isSecondTapOnSamePoi =
                string.Equals(_lastMarkerTapPoiId, poi.PoiId, StringComparison.OrdinalIgnoreCase)
                && (nowUtc - _lastMarkerTapUtc) <= MarkerDoubleTapWindow;

            _lastMarkerTapPoiId = poi.PoiId;
            _lastMarkerTapUtc = nowUtc;

            // Khóa popup mặc định của marker để bắt buộc user chạm lần 2 mới mở card chi tiết.
            e.HideInfoWindow = true;

            if (!isSecondTapOnSamePoi)
            {
                _selectedPoi = poi;
                HideSelectedPoiCard();
                RenderPoiPins();
                MoveMapToPoi(poi, 320);
                _viewModel.SetStatusMessage($"Nhấn lại marker '{poi.Name}' để mở chi tiết.");
                return;
            }

            _lastMarkerTapPoiId = null;
            _lastMarkerTapUtc = DateTimeOffset.MinValue;

            SetSelectedPoi(poi);
            await _viewModel.OnPoiSelectedAsync(poi);
            MoveMapToPoi(poi, 320);
        }

        private void HideSelectedPoiCard()
        {
            SelectedPoiOverlay.IsVisible = false;
            SelectedPoiOverlay.Opacity = 0;
            SelectedPoiNameLabel.Text = string.Empty;
            SelectedPoiDistanceLabel.Text = string.Empty;
            SelectedPoiDescriptionLabel.Text = string.Empty;
            SelectedPoiImage.Source = null;
            SelectedPoiImage.IsVisible = false;
            SelectedPoiNoImageLabel.IsVisible = true;
            SelectedPoiRichContentView.Source = new HtmlWebViewSource { Html = "<html><body></body></html>" };
            SelectedPoiRichContentContainer.IsVisible = false;
        }

        private void ApplySelectedPoiRichContent(string richContent)
        {
            var htmlDocument = BuildRichContentDocument(richContent);
            if (string.IsNullOrWhiteSpace(htmlDocument))
            {
                SelectedPoiRichContentView.Source = new HtmlWebViewSource { Html = "<html><body></body></html>" };
                SelectedPoiRichContentContainer.IsVisible = false;
                return;
            }

            SelectedPoiRichContentView.Source = new HtmlWebViewSource
            {
                Html = htmlDocument
            };
            SelectedPoiRichContentContainer.IsVisible = true;
        }

        private static string BuildRichContentDocument(string richContent)
        {
            var normalized = richContent?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var sanitized = ScriptTagRegex.Replace(normalized, string.Empty);
            sanitized = StyleTagRegex.Replace(sanitized, string.Empty);
            sanitized = OnEventAttributeRegex.Replace(sanitized, string.Empty);
            sanitized = JavascriptHrefRegex.Replace(sanitized, string.Empty);

            var looksLikeHtml = HtmlTagRegex.IsMatch(sanitized);
            var safeBody = looksLikeHtml
                ? sanitized
                : $"<p>{WebUtility.HtmlEncode(sanitized).Replace("\n", "<br/>")}</p>";

                        return "<!DOCTYPE html>"
                                     + "<html><head>"
                                     + "<meta charset=\"utf-8\" />"
                                     + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\" />"
                                     + "<style>"
                                     + "body { margin: 0; font-family: 'Segoe UI', sans-serif; color: #275355; line-height: 1.45; font-size: 14px; }"
                                     + "p { margin: 0 0 10px 0; }"
                                     + "ul, ol { margin: 0 0 10px 20px; padding: 0; }"
                                     + "h1, h2, h3, h4 { margin: 0 0 8px 0; color: #154D50; }"
                                     + "img { max-width: 100%; height: auto; border-radius: 8px; }"
                                     + "a { color: #0B949A; }"
                                     + "</style></head><body>"
                                     + safeBody
                                     + "</body></html>";
        }

        private void ApplySelectedPoiImage(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                SelectedPoiImage.Source = null;
                SelectedPoiImage.IsVisible = false;
                SelectedPoiNoImageLabel.IsVisible = true;
                return;
            }

            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri)
                || (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            {
                SelectedPoiImage.Source = null;
                SelectedPoiImage.IsVisible = false;
                SelectedPoiNoImageLabel.IsVisible = true;
                return;
            }

            // Chỉ tải ảnh từ URL lưu trong PostgreSQL, không phụ thuộc ảnh local trong app package.
            SelectedPoiImage.Source = ImageSource.FromUri(imageUri);
            SelectedPoiImage.IsVisible = true;
            SelectedPoiNoImageLabel.IsVisible = false;
        }

        private void ApplyBottomSheetRatio(double bottomRatio)
        {
            var clampedRatio = Math.Clamp(bottomRatio, 0.15, 0.85);
            MainRootGrid.RowDefinitions[0].Height = new GridLength(1 - clampedRatio, GridUnitType.Star);
            MainRootGrid.RowDefinitions[1].Height = new GridLength(clampedRatio, GridUnitType.Star);
        }

        private Task AnimateBottomSheetRatioAsync(double targetRatio)
        {
            if (_isAnimatingSheet)
            {
                this.AbortAnimation("PoiBottomSheetAnimation");
                _isAnimatingSheet = false;
            }

            _isAnimatingSheet = true;
            var completionSource = new TaskCompletionSource();
            var startRatio = _bottomSheetRatio;
            var clampedTarget = Math.Clamp(targetRatio, 0.15, 0.85);

            // Animate ty le de tao hieu ung bottom sheet truot len xuong muot hon.
            var animation = new Animation(
                callback: ratio =>
                {
                    _bottomSheetRatio = ratio;
                    ApplyBottomSheetRatio(ratio);
                },
                start: startRatio,
                end: clampedTarget);

            animation.Commit(
                owner: this,
                name: "PoiBottomSheetAnimation",
                rate: 16,
                length: 260,
                easing: Easing.CubicInOut,
                finished: (_, _) =>
                {
                    _isAnimatingSheet = false;
                    completionSource.SetResult();
                });

            return completionSource.Task;
        }


        private async void OnMapClicked(object? sender, MapClickedEventArgs e)
        {
            // Chạm vào bản đồ thì thu gọn bottom sheet để bản đồ trở thành vùng hiển thị chính.
            if (_bottomSheetRatio > CollapsedBottomRatio + 0.1)
            {
                await MinimizeBottomSheetAsync();
            }
        }

        private async void OnNavigateToSelectedPoiClicked(object? sender, EventArgs e)
        {
            if (_selectedPoi is null)
            {
                await DisplayAlertAsync("Directions", "Hãy chọn POI trước khi mở chỉ đường.", "OK");
                return;
            }

            try
            {
                var destination = new Location(_selectedPoi.Latitude, _selectedPoi.Longitude);
                await AppModel.Map.OpenAsync(destination, new AppModel.MapLaunchOptions
                {
                    Name = _selectedPoi.Name,
                    NavigationMode = AppModel.NavigationMode.Driving
                });

                // Bắt đầu một lượt điều hướng mới để chỉ cộng tour khi user thực sự đến trong bán kính 20m.
                BeginRouteArrivalSession(_selectedPoi);
                _viewModel.SetStatusMessage($"Đang theo dõi đến nơi: {_selectedPoi.Name} (<= {TourArrivalRadiusMeters:0}m).");
            }
            catch
            {
                await DisplayAlertAsync("Directions", "Không thể mở ứng dụng chỉ đường lúc này.", "OK");
            }
        }

        private void OnCloseSelectedPoiClicked(object? sender, EventArgs e)
        {
            _selectedPoi = null;
            HideSelectedPoiCard();
            RenderPoiPins();
        }

        private async void OnSettingsClicked(object? sender, EventArgs e)
        {
            if (!_viewModel.IsAuthenticatedUser)
            {
                await DisplayAlertAsync("Settings", "Please sign in to edit profile and security settings.", "OK");
                return;
            }

            // Điều hướng sang màn hình cài đặt hồ sơ cho người dùng đã xác thực.
            await Navigation.PushAsync(new SettingsPage());
        }

        private async void OnBottomSheetTapped(object? sender, TappedEventArgs e)
        {
            // Chạm vào khu vực bottom sheet thì mở rộng để ưu tiên thao tác POI/Camera.
            if (_bottomSheetRatio < ExpandedBottomRatio - 0.1)
            {
                await ExpandBottomSheetAsync();
            }
        }

        private async void OnDragHandleTapped(object? sender, TappedEventArgs e)
        {
            // Chạm vào thanh kéo để chuyển nhanh giữa hai trạng thái mở rộng và thu gọn.
            if (_bottomSheetRatio > 0.5)
            {
                // Đang mở rộng thì thu gọn lại để nhường diện tích cho bản đồ.
                await MinimizeBottomSheetAsync();
            }
            else
            {
                // Đang thu gọn thì mở rộng để thao tác danh sách POI/Camera dễ hơn.
                await ExpandBottomSheetAsync();
            }
        }
        private async void OnBottomSheetSwipedUp()
        {
            // Vuốt lên để mở rộng bottom sheet.
            await ExpandBottomSheetAsync();
        }

        private async void OnBottomSheetSwipedDown()
        {
            // Vuốt xuống để thu gọn bottom sheet.
            await MinimizeBottomSheetAsync();
        }

#if ANDROID
        private async Task EnsureLocationTrackingAsync()
        {
            if (_isLocationTrackingStarted)
            {
                return;
            }

            try
            {
                // Xin quyền vị trí trước khi bật tracking nền để tự động cập nhật khi người dùng di chuyển.
                var isGranted = await _locationService.RequestLocationPermissionsAsync();
                if (!isGranted)
                {
                    _viewModel.SetStatusMessage("Chưa được cấp quyền vị trí, không thể tự động theo dõi.");
                    return;
                }

                await _locationService.StartTrackingAsync();
                _isLocationTrackingStarted = true;
            }
            catch
            {
                _viewModel.SetStatusMessage("Không thể khởi động theo dõi vị trí tự động.");
            }
        }
#endif

        private Task ExpandBottomSheetAsync()
        {
            // Áp dụng animation thay đổi RowDefinitions để phần dưới chiếm nhiều diện tích hơn.
            return AnimateBottomSheetRatioAsync(ExpandedBottomRatio);
        }

        private Task MinimizeBottomSheetAsync()
        {
            // Áp dụng animation thay đổi RowDefinitions để bản đồ trở thành vùng hiển thị chính.
            return AnimateBottomSheetRatioAsync(CollapsedBottomRatio);
        }

        private void BeginRouteArrivalSession(PoiListItemViewModel destinationPoi)
        {
            StopRouteArrivalMonitor(clearSessionState: true);

            _activeRouteDestination = new Location(destinationPoi.Latitude, destinationPoi.Longitude);
            _activeRoutePoiId = destinationPoi.PoiId;
            _activeRoutePoiName = destinationPoi.Name;
            _isRouteArrivalSavedForCurrentSession = false;

            _routeArrivalMonitorCts = new CancellationTokenSource();
            _ = MonitorRouteArrivalAsync(_routeArrivalMonitorCts.Token);
        }

        private void StopRouteArrivalMonitor(bool clearSessionState)
        {
            if (_routeArrivalMonitorCts is not null)
            {
                _routeArrivalMonitorCts.Cancel();
                _routeArrivalMonitorCts.Dispose();
                _routeArrivalMonitorCts = null;
            }

            if (!clearSessionState)
            {
                return;
            }

            _activeRouteDestination = null;
            _activeRoutePoiId = null;
            _activeRoutePoiName = null;
            _isRouteArrivalSavedForCurrentSession = false;
            _isSavingRouteArrival = false;
        }

        private async Task MonitorRouteArrivalAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var destination = _activeRouteDestination;
                var activePoiId = _activeRoutePoiId;
                if (destination is null || string.IsNullOrWhiteSpace(activePoiId))
                {
                    return;
                }

                try
                {
                    var currentLocation = await _locationService.GetLastKnownLocationAsync(cancellationToken);
                    if (currentLocation is not null)
                    {
                        var distanceKm = Location.CalculateDistance(currentLocation, destination, DistanceUnits.Kilometers);
                        var distanceMeters = distanceKm * 1000d;
                        var isInsideArrivalRadius = distanceMeters <= TourArrivalRadiusMeters;

                        if (!isInsideArrivalRadius)
                        {
                            // Khi user đã ra khỏi vùng 20m, mở khóa để lượt ghé kế tiếp có thể được cộng tour lại.
                            _routeArrivalLatchByPoiId.Remove(activePoiId);

                            if (_isRouteArrivalSavedForCurrentSession)
                            {
                                StopRouteArrivalMonitor(clearSessionState: true);
                                return;
                            }
                        }
                        else if (!_isRouteArrivalSavedForCurrentSession
                                 && !_routeArrivalLatchByPoiId.Contains(activePoiId))
                        {
                            _routeArrivalLatchByPoiId.Add(activePoiId);
                            await SaveRouteArrivalTourAsync(activePoiId, _activeRoutePoiName ?? activePoiId, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Bỏ qua lỗi tức thời của GPS để vòng kiểm tra kế tiếp vẫn tiếp tục.
                }

                try
                {
                    await Task.Delay(RouteArrivalPollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task SaveRouteArrivalTourAsync(string poiId, string poiName, CancellationToken cancellationToken)
        {
            if (_isSavingRouteArrival)
            {
                return;
            }

            _isSavingRouteArrival = true;
            try
            {
                var currentUserId = _userSessionService.CurrentUserId;
                if (_userSessionService.IsGuest || !currentUserId.HasValue)
                {
                    _isRouteArrivalSavedForCurrentSession = true;

                    await AppModel.MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _viewModel.SetStatusMessage($"Đã đến {poiName} trong 20m. Đăng nhập để lưu tour.");
                    });

                    return;
                }

                await _userActivityRepository.AddTourAsync(
                    currentUserId.Value,
                    poiId,
                    _viewModel.SelectedLanguage,
                    "route_arrival",
                    cancellationToken);

                _isRouteArrivalSavedForCurrentSession = true;

                await AppModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var visitedPoi = _viewModel.PoiItems.FirstOrDefault(x =>
                        string.Equals(x.PoiId, poiId, StringComparison.OrdinalIgnoreCase));

                    if (visitedPoi is not null)
                    {
                        visitedPoi.IsVisited = true;
                    }

                    if (_selectedPoi is not null
                        && string.Equals(_selectedPoi.PoiId, poiId, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedPoi.IsVisited = true;
                    }

                    _viewModel.SetStatusMessage($"Đã ghi nhận tour đến nơi: {poiName} (<= {TourArrivalRadiusMeters:0}m).");
                });
            }
            catch
            {
                _isRouteArrivalSavedForCurrentSession = true;

                await AppModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _viewModel.SetStatusMessage("Không thể lưu tour đến nơi lúc này.");
                });
            }
            finally
            {
                _isSavingRouteArrival = false;
            }
        }
    }
}
