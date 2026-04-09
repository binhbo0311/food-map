namespace FOOD_MAP.Shared.Models;

public class SyncHistory
{
    public int Id { get; set; }

    public string SyncType { get; set; } = string.Empty;

    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }
}