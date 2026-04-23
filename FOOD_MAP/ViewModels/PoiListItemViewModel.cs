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
        bool isNearest = false)
    {
        PoiId = poiId;
        PoiType = poiType;
        Latitude = latitude;
        Longitude = longitude;
        ActivationRadius = activationRadius;
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
