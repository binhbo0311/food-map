using System.Collections.Generic;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Utilities;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ZXing.Net.Maui;

namespace FOOD_MAP;

public partial class CameraPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;
    private bool _isProcessing;

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
        BarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    public string StatusMessage { get; private set; } = "Hãy đưa mã QR vào trong khung để quét.";

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
            QrPayloadEntry.Text = barcodeValue;
            StatusMessage = $"Đã nhận QR: {barcodeValue}";
            OnPropertyChanged(nameof(StatusMessage));
        });

        await ProcessQrAsync(barcodeValue);
    }

    private async void OnQrPayloadChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isProcessing)
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