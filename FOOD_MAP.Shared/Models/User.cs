namespace FOOD_MAP.Shared.Models;

public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    // Mã định danh owner để link với business profile sau khi admin duyệt.
    public string? OwnerIdentificationCode { get; set; }

    public DateTimeOffset? OwnerApprovedUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    // Lưu danh sách POI user đã đánh dấu yêu thích.
    public ICollection<UserFavorite> Favorites { get; set; } = new List<UserFavorite>();

    // Lưu lịch sử các điểm user đã nghe trong tour.
    public ICollection<UserTour> Tours { get; set; } = new List<UserTour>();

    public ICollection<OwnerRegistrationRequest> OwnerRegistrationRequestsSubmitted { get; set; } = new List<OwnerRegistrationRequest>();

    public ICollection<OwnerRegistrationRequest> OwnerRegistrationRequestsReviewed { get; set; } = new List<OwnerRegistrationRequest>();

    public ICollection<POI> OwnedPois { get; set; } = new List<POI>();

    public ICollection<POI> ReviewedPois { get; set; } = new List<POI>();

    public ICollection<FoodItem> ManagedFoodItems { get; set; } = new List<FoodItem>();

    public ICollection<LanguageOwnershipRequest> LanguageOwnershipRequestsSubmitted { get; set; } = new List<LanguageOwnershipRequest>();

    public ICollection<LanguageOwnershipRequest> LanguageOwnershipRequestsReviewed { get; set; } = new List<LanguageOwnershipRequest>();
}
