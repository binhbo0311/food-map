using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

[QueryProperty(nameof(InitialTourCode), "tourCode")]
public partial class TourBuilderPage : ContentPage
{
    private readonly IPoiRepository _poiRepository;
    private readonly IUserSessionService _userSessionService;
    private readonly string _languageCode;
    private bool _isLoading;
    private bool _isLoaded;
    private string? _initialTourCode;

    public TourBuilderPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _poiRepository = services.GetRequiredService<IPoiRepository>();
        _userSessionService = services.GetRequiredService<IUserSessionService>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");

        BindingContext = this;
        TourName = "custom_tour";
    }

    public ObservableCollection<TourBuilderPoiItem> CandidatePois { get; } = [];

    public string? InitialTourCode
    {
        get => _initialTourCode;
        set => _initialTourCode = string.IsNullOrWhiteSpace(value)
            ? null
            : Uri.UnescapeDataString(value).Trim().ToUpperInvariant();
    }

    public bool IsEditMode => !string.IsNullOrWhiteSpace(InitialTourCode);

    public bool CanPublishPublicTour => _userSessionService.CurrentRole is UserRole.Owner or UserRole.Admin;

    public string PublishHintText => CanPublishPublicTour
        ? "Owner/Admin có thể tạo public tour"
        : "User thường chỉ tạo private tour";

    private string _tourName = string.Empty;
    public string TourName
    {
        get => _tourName;
        set
        {
            if (_tourName == value)
            {
                return;
            }

            _tourName = value;
            OnPropertyChanged();
        }
    }

    private bool _isPublic;
    public bool IsPublic
    {
        get => _isPublic;
        set
        {
            var normalized = CanPublishPublicTour && value;
            if (_isPublic == normalized)
            {
                return;
            }

            _isPublic = normalized;
            OnPropertyChanged();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded)
        {
            return;
        }

        await LoadDataAsync();
        _isLoaded = true;
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var poiItems = await _poiRepository.GetPoiItemsAsync(_languageCode);

            CandidatePois.Clear();
            foreach (var poi in poiItems)
            {
                CandidatePois.Add(new TourBuilderPoiItem(poi));
            }

            if (IsEditMode && !string.IsNullOrWhiteSpace(InitialTourCode))
            {
                var existingTourPois = await _poiRepository.GetTourPoiItemsAsync(InitialTourCode, _languageCode);
                var selectedPoiIds = existingTourPois.Select(x => x.PoiId).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var poi in CandidatePois)
                {
                    poi.IsSelected = selectedPoiIds.Contains(poi.PoiId);
                }

                TourName = BuildEditableName(InitialTourCode);
                IsPublic = InitialTourCode.StartsWith("PUB_", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                IsPublic = false;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var selectedPoiIds = CandidatePois
            .Where(x => x.IsSelected)
            .Select(x => x.PoiId)
            .ToList();

        if (selectedPoiIds.Count == 0)
        {
            await DisplayAlertAsync("Tour", "Hãy chọn ít nhất một POI.", "OK");
            return;
        }

        try
        {
            TourSummaryViewModel? savedTour;
            if (IsEditMode && !string.IsNullOrWhiteSpace(InitialTourCode))
            {
                savedTour = await _poiRepository.UpdateTourAsync(InitialTourCode, TourName, selectedPoiIds);
            }
            else
            {
                savedTour = await _poiRepository.CreateTourAsync(TourName, selectedPoiIds, IsPublic);
            }

            if (savedTour is null)
            {
                await DisplayAlertAsync("Tour", "Không thể lưu tour lúc này.", "OK");
                return;
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"{nameof(TourPage)}?tourCode={Uri.EscapeDataString(savedTour.TourCode)}");
                return;
            }

            await Navigation.PushAsync(new TourPage());
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Tour", $"Lưu tour thất bại: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await Navigation.PopAsync();
    }

    private static string BuildEditableName(string tourCode)
    {
        if (string.IsNullOrWhiteSpace(tourCode))
        {
            return "custom_tour";
        }

        var normalized = tourCode.Trim().ToUpperInvariant();
        var withoutPrefix = normalized;

        if (normalized.StartsWith("PUB_", StringComparison.OrdinalIgnoreCase))
        {
            withoutPrefix = normalized[4..];
        }
        else if (normalized.StartsWith("USR_", StringComparison.OrdinalIgnoreCase))
        {
            var secondBoundary = normalized.IndexOf('_', 4);
            withoutPrefix = secondBoundary > 0 && secondBoundary + 1 < normalized.Length
                ? normalized[(secondBoundary + 1)..]
                : normalized;
        }

        var lastUnderscore = withoutPrefix.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            withoutPrefix = withoutPrefix[..lastUnderscore];
        }

        return withoutPrefix.ToLowerInvariant();
    }

    public sealed class TourBuilderPoiItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public TourBuilderPoiItem(PoiListItemViewModel poi)
        {
            PoiId = poi.PoiId;
            Name = poi.Name;
            Description = poi.Description;
            ImageUrl = poi.ImageUrl;
            PoiTypeText = poi.IsFoodPoi ? "Food" : "Visit";
        }

        public string PoiId { get; }

        public string Name { get; }

        public string Description { get; }

        public string ImageUrl { get; }

        public string PoiTypeText { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
