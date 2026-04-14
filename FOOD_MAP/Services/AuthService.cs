using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Services;

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IUserSessionService _userSessionService;
    private readonly IDataService _dataService;
    private readonly IUserActivityRepository _userActivityRepository;

    public AuthService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IUserSessionService userSessionService,
        IDataService dataService,
        IUserActivityRepository userActivityRepository)
    {
        _dbContextFactory = dbContextFactory;
        _userSessionService = userSessionService;
        _dataService = dataService;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<(bool IsSuccess, string Message)> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = TextInputNormalizer.NormalizeSingleLine(userName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Vui lòng nhập đầy đủ tài khoản và mật khẩu.");
        }

        // Đảm bảo database và tài khoản demo được khởi tạo trước khi xác thực.
        await _dataService.SeedDataAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var passwordHash = PasswordHasher.Hash(password);

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserName.ToLower() == normalizedUserName && x.PasswordHash == passwordHash,
                cancellationToken);

        if (user is null)
        {
            return (false, "Thông tin đăng nhập không đúng.");
        }

        _userSessionService.SignIn(user.Id, user.DisplayName, user.Role);
        return (true, $"Xin chào {user.DisplayName}.");
    }

    public async Task<(bool IsSuccess, string Message)> RegisterAsync(string userName, string displayName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = TextInputNormalizer.NormalizeSingleLine(userName).ToLowerInvariant();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? TextInputNormalizer.NormalizeSingleLine(userName)
            : TextInputNormalizer.NormalizeSingleLine(displayName);

        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Vui lòng nhập đầy đủ tài khoản và mật khẩu.");
        }

        if (password.Length < 6)
        {
            return (false, "Mật khẩu phải có ít nhất 6 ký tự.");
        }

        // Đảm bảo database đã sẵn sàng trước khi tạo tài khoản mới.
        await _dataService.SeedDataAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var userNameExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.UserName.ToLower() == normalizedUserName, cancellationToken);

        if (userNameExists)
        {
            return (false, "Tên tài khoản đã tồn tại. Vui lòng chọn tên khác.");
        }

        dbContext.Users.Add(new User
        {
            UserName = normalizedUserName,
            DisplayName = normalizedDisplayName,
            PasswordHash = PasswordHasher.Hash(password),
            CreatedUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Tạo tài khoản thành công. Bạn có thể đăng nhập ngay bây giờ.");
    }

    public Task ContinueAsGuestAsync(CancellationToken cancellationToken = default)
    {
        // Guest mode không giữ dữ liệu cá nhân, xóa cache local trước khi chuyển session.
        return ContinueAsGuestInternalAsync(cancellationToken);
    }

    private async Task ContinueAsGuestInternalAsync(CancellationToken cancellationToken)
    {
        await _userActivityRepository.ClearLocalCacheAsync(cancellationToken);
        _userSessionService.UseGuestMode();
    }
}
