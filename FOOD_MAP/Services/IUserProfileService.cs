namespace FOOD_MAP.Services;

public interface IUserProfileService
{
    Task<UserProfileSnapshot?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> UpdateDisplayNameAsync(int userId, string displayName, CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
