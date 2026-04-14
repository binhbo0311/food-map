namespace FOOD_MAP.ViewModels;

public sealed class FoodMenuItemViewModel
{
    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    public decimal Price { get; }

    public string Currency { get; }

    public bool IsAvailable { get; }

    public FoodMenuItemViewModel(int id, string name, string description, decimal price, string currency, bool isAvailable)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        IsAvailable = isAvailable;
    }

    public string PriceText => $"{Price:0.##} {Currency}";

    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";
}
