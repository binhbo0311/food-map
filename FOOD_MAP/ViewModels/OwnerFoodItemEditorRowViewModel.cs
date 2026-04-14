namespace FOOD_MAP.ViewModels;

public sealed class OwnerFoodItemEditorRowViewModel
{
    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    public decimal Price { get; }

    public string Currency { get; }

    public bool IsAvailable { get; }

    public int DisplayOrder { get; }

    public OwnerFoodItemEditorRowViewModel(
        int id,
        string name,
        string description,
        decimal price,
        string currency,
        bool isAvailable,
        int displayOrder)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        IsAvailable = isAvailable;
        DisplayOrder = displayOrder;
    }

    public string PriceText => $"{Price:0.##} {Currency}";

    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";
}
