using System.ComponentModel;
using System.Runtime.CompilerServices;
using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.ViewModels;

public sealed class PoiListItemViewModel : INotifyPropertyChanged
{
    private bool _isNearest;
    private bool _isFavorite;
    private bool _isVisited;

    public string PoiId { get; }

    public PoiType PoiType { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public int ActivationRadius { get; }

    public int Priority { get; }

    public string Name { get; }

    public string DistanceText { get; }

    public string Description { get; }

    public string NarrationText { get; }

    public string ImageUrl { get; }

    public string RichContentHtml { get; }

    public PoiListItemViewModel(
        string poiId,
        PoiType poiType,
        double latitude,
        double longitude,
        string name,
        string distanceText,
        string description,
        string narrationText,
        string imageUrl,
        string richContentHtml,
        int activationRadius = 100,
        int priority = 0,
        bool isNearest = false)
    {
        PoiId = poiId;
        PoiType = poiType;
        Latitude = latitude;
        Longitude = longitude;
        ActivationRadius = activationRadius;
        Priority = priority;
        Name = name;
        DistanceText = distanceText;
        Description = description;
        NarrationText = narrationText;
        ImageUrl = imageUrl;
        RichContentHtml = richContentHtml;
        _isNearest = isNearest;
    }

    public bool IsNearest
    {
        get => _isNearest;
        set
        {
            if (_isNearest == value)
            {
                return;
            }

            _isNearest = value;
            OnPropertyChanged();
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
            {
                return;
            }

            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteButtonText));
        }
    }

    public string FavoriteButtonText => _isFavorite ? "Unfav" : "Fav";

    public bool IsFoodPoi => PoiType == PoiType.Food;

    public bool IsVisited
    {
        get => _isVisited;
        set
        {
            if (_isVisited == value)
            {
                return;
            }

            _isVisited = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TourSummaryViewModel
{
    public TourSummaryViewModel(string tourCode, int poiCount, string? tourName = null, int? ownerUserId = null, bool? isPublic = null)
    {
        TourCode = string.IsNullOrWhiteSpace(tourCode) ? "DEFAULT" : tourCode.Trim().ToUpperInvariant();
        PoiCount = poiCount;
        OwnerUserId = ownerUserId;

        IsPublic = isPublic ?? TourCode.StartsWith("PUB_", StringComparison.OrdinalIgnoreCase);
        IsPrivate = TourCode.StartsWith("USR_", StringComparison.OrdinalIgnoreCase);

        var displayCore = string.IsNullOrWhiteSpace(tourName) ? TourCode : tourName.Trim();
        if (string.IsNullOrWhiteSpace(tourName))
        {
            if (IsPublic)
            {
                displayCore = TourCode[4..];
            }
            else if (IsPrivate)
            {
                var secondUnderscoreIndex = TourCode.IndexOf('_', 4);
                if (secondUnderscoreIndex > 0 && secondUnderscoreIndex + 1 < TourCode.Length)
                {
                    displayCore = TourCode[(secondUnderscoreIndex + 1)..];
                }
            }
        }

        DisplayName = string.IsNullOrWhiteSpace(displayCore)
            ? "Tour"
            : displayCore.Replace('_', ' ');
    }

    public string TourCode { get; }

    public bool IsPublic { get; }

    public bool IsPrivate { get; }

    public int? OwnerUserId { get; }

    public string DisplayName { get; }

    public int PoiCount { get; }

    public string SummaryText => IsPublic
        ? $"Public • {PoiCount} POI"
        : $"Private • {PoiCount} POI";
}
