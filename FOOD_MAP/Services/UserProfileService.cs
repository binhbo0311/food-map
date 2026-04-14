using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Services;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public UserProfileService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<UserProfileSnapshot?> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var profile = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileSnapshot(x.Id, x.UserName, x.DisplayName, x.Role, x.OwnerIdentificationCode))
            .FirstOrDefaultAsync(cancellationToken);

        return profile;
    }

    public async Task<(bool IsSuccess, string Message)> UpdateDisplayNameAsync(int userId, string displayName, CancellationToken cancellationToken = default)
    {
        var normalizedDisplayName = TextInputNormalizer.NormalizeSingleLine(displayName);
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            return (false, "Display name cannot be empty.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return (false, "User was not found.");
        }

        user.DisplayName = normalizedDisplayName;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, "Profile updated successfully.");
    }

    public async Task<(bool IsSuccess, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return (false, "Please fill in both current and new passwords.");
        }

        if (newPassword.Length < 6)
        {
            return (false, "New password must be at least 6 characters.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return (false, "User was not found.");
        }

        var currentPasswordHash = PasswordHasher.Hash(currentPassword);
        if (!string.Equals(user.PasswordHash, currentPasswordHash, StringComparison.Ordinal))
        {
            return (false, "Current password is incorrect.");
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, "Password changed successfully.");
    }
}
