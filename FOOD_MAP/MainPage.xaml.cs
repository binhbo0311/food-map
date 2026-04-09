using FOOD_MAP.Services;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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

        private readonly MainPageViewModel _viewModel;
        private readonly ILocationService _locationService;
        private PoiListItemViewModel? _selectedPoi;
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

        private void OnPoiSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not PoiListItemViewModel selectedPoi)
            {
                return;
            }

            SetSelectedPoi(selectedPoi);

            // Tu dong dua camera ban do den POI vua chon de nguoi dung khong can tu keo tay.
            MoveMapToPoi(selectedPoi, 400);

            // Cuon item duoc chon len vi tri giua man hinh de nguoi dung nhin ro hon.
            PoiCollectionView.ScrollTo(selectedPoi, position: ScrollToPosition.Center, animate: true);

            // Bo chon de lan cham tiep theo van kich hoat scroll lai.
            PoiCollectionView.SelectedItem = null;
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

            if (!SelectedPoiOverlay.IsVisible)
            {
                SelectedPoiOverlay.Opacity = 0;
                SelectedPoiOverlay.IsVisible = true;
                _ = SelectedPoiOverlay.FadeToAsync(1, 180, Easing.CubicOut);
            }

            RenderPoiPins();
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
    }
}
