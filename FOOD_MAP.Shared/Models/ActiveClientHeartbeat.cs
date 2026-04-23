namespace FOOD_MAP.Shared.Models;

public class ActiveClientHeartbeat
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ClientType { get; set; } = "web";

    public int? UserId { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;

    public string SessionKey { get; set; } = string.Empty;
}
