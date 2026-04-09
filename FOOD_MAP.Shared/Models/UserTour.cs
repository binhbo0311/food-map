namespace FOOD_MAP.Shared.Models;

public class UserTour
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int PoiId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string TriggerType { get; set; } = string.Empty;

    public DateTimeOffset VisitedUtc { get; set; }

    public User? User { get; set; }

    public POI? Poi { get; set; }
}
