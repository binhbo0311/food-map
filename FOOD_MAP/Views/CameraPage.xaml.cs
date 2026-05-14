using System.Collections.Generic;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Utilities;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using ZXing.Net.Maui;

namespace FOOD_MAP;

public partial class CameraPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private readonly SemaphoreSlim _cameraInitGate = new(1, 1);
    private bool _isProcessing;
    private bool _cameraInitialized;
    private bool _suppressQrPayloadChange;
    private string? _pendingExternalPayload;

    public CameraPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _viewModel = new MainPageViewModel(
            services.GetRequiredService<IPoiRepository>(),
            services.GetRequiredService<INarrationService>(),
            services.GetRequiredService<IDataService>(),
            services.GetRequiredService<IUserSessionService>(),
            services.GetRequiredService<IUserActivityRepository>());

        BindingContext = this;
    }

    public string StatusMessage { get; private set; } = "Hãy đưa mã QR vào trong khung để quét.";

    public async Task HandleExternalPayloadAsync(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        _pendingExternalPayload = payload.Trim();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _suppressQrPayloadChange = true;
            QrPayloadEntry.Text = _pendingExternalPayload;
            _suppressQrPayloadChange = false;
            StatusMessage = $"Đã nhận QR: {_pendingExternalPayload}";
            OnPropertyChanged(nameof(StatusMessage));
        });

        await ProcessPendingExternalPayloadAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await EnsureCameraReadyAsync();
        await ProcessPendingExternalPayloadAsync();
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await EnsureCameraReadyAsync();
        await ProcessPendingExternalPayloadAsync();
    }

    private async Task EnsureCameraReadyAsync()
    {
        await _cameraInitGate.WaitAsync();
        try
        {
            if (_cameraInitialized)
            {
                BarcodeReaderView.IsDetecting = true;
                return;
            }

            if (!BarcodeScanning.IsSupported)
            {
                StatusMessage = "Thiết bị này không hỗ trợ quét camera.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permissionStatus != PermissionStatus.Granted)
            {
                permissionStatus = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (permissionStatus != PermissionStatus.Granted)
            {
                StatusMessage = "Cần cấp quyền camera để quét QR.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            BarcodeReaderView.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false
            };
            BarcodeReaderView.CameraLocation = CameraLocation.Rear;

            var cameras = await BarcodeReaderView.GetAvailableCameras();
            if (cameras.Count == 0)
            {
                StatusMessage = "Không tìm thấy camera khả dụng trên thiết bị.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            var rearCamera = cameras.FirstOrDefault(camera => camera.Location == CameraLocation.Rear);
            BarcodeReaderView.SelectedCamera = rearCamera ?? cameras[0];
            BarcodeReaderView.IsDetecting = true;
            _cameraInitialized = true;

            StatusMessage = $"Camera sẵn sàng ({cameras.Count} camera). Đưa QR vào khung để quét.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        finally
        {
            _cameraInitGate.Release();
        }
    }

    protected override void OnDisappearing()
    {
        BarcodeReaderView.IsDetecting = false;
        _cameraInitialized = false;
        base.OnDisappearing();
    }

    private async Task ProcessPendingExternalPayloadAsync()
    {
        if (!_cameraInitialized || _isProcessing || string.IsNullOrWhiteSpace(_pendingExternalPayload))
        {
            return;
        }

        var pendingPayload = _pendingExternalPayload;
        _pendingExternalPayload = null;
        await ProcessQrAsync(pendingPayload);
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing)
        {
            return;
        }

        var barcodeValue = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(barcodeValue))
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _suppressQrPayloadChange = true;
            QrPayloadEntry.Text = barcodeValue;
            _suppressQrPayloadChange = false;
            StatusMessage = $"Đã nhận QR: {barcodeValue}";
            OnPropertyChanged(nameof(StatusMessage));
        });

        await ProcessQrAsync(barcodeValue);
    }

    private async void OnQrPayloadChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isProcessing || _suppressQrPayloadChange)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            return;
        }

        if (QrPayloadParser.ExtractPoiIdFromPayload(e.NewTextValue) is null)
        {
            return;
        }

        await ProcessQrAsync(e.NewTextValue);
    }

    private async Task ProcessQrAsync(string rawPayload)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            var poiId = QrPayloadParser.ExtractPoiIdFromPayload(rawPayload);
            if (string.IsNullOrWhiteSpace(poiId))
            {
                StatusMessage = "QR không hợp lệ.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            var languageOptions = await _viewModel.GetAvailableLanguagesForQrAsync(poiId);
            if (languageOptions.Count == 0)
            {
                StatusMessage = $"Không tìm thấy bản dịch cho POI '{poiId}'.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            var selectedLanguage = languageOptions.FirstOrDefault(x => string.Equals(x.LanguageCode, _viewModel.SelectedLanguage, StringComparison.OrdinalIgnoreCase))
                ?? languageOptions[0];

            var scanResult = await _viewModel.HandleQrScanAsync(poiId, selectedLanguage.LanguageCode);
            StatusMessage = scanResult is null
                ? $"Không thể xử lý QR '{poiId}'."
                : $"Đang phát TTS cho {scanResult.LocationName}.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể xử lý QR: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async void OnProcessClicked(object? sender, EventArgs e)
    {
        await ProcessQrAsync(QrPayloadEntry.Text);
    }

    private async void OnStopClicked(object? sender, EventArgs e)
    {
        await _viewModel.StopTtsAsyncForCamera();
        StatusMessage = "Đã dừng TTS.";
        OnPropertyChanged(nameof(StatusMessage));
    }
}
