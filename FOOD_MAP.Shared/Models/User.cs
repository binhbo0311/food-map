namespace FOOD_MAP.Shared.Models;

public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    // Lưu danh sách POI user đã đánh dấu yêu thích.
    public ICollection<UserFavorite> Favorites { get; set; } = new List<UserFavorite>();

    // Lưu lịch sử các điểm user đã nghe trong tour.
    public ICollection<UserTour> Tours { get; set; } = new List<UserTour>();
}
