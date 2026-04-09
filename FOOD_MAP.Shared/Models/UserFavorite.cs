namespace FOOD_MAP.Shared.Models;

public class UserFavorite
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int PoiId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public User? User { get; set; }

    public POI? Poi { get; set; }
}
