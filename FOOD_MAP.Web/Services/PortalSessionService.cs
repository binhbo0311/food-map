using System.Security.Cryptography;
using System.Text;
using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Web.Services;

public sealed record PortalSignInResult(bool IsSuccess, string Message, UserRole? Role);

public sealed class PortalSessionService : IAsyncDisposable
{
    private const string SessionStorageKey = "foodmap.portal.session";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly PortalUserSessionState _portalUserSessionState;
    private readonly IActiveUserTrackerService _activeUserTrackerService;

    private readonly string _sessionKey = $"web-{Guid.NewGuid():N}";
    private CancellationTokenSource? _heartbeatLoopCts;
    private Task? _heartbeatLoopTask;
    private bool _isInitialized;

    public PortalSessionService(
        ProtectedSessionStorage sessionStorage,
        IDbContextFactory<AppDbContext> dbContextFactory,
        PortalUserSessionState portalUserSessionState,
        IActiveUserTrackerService activeUserTrackerService)
    {
        _sessionStorage = sessionStorage;
        _dbContextFactory = dbContextFactory;
        _portalUserSessionState = portalUserSessionState;
        _activeUserTrackerService = activeUserTrackerService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        try
        {
            var storedSession = await _sessionStorage.GetAsync<PortalSessionSnapshot>(SessionStorageKey);
            if (!storedSession.Success || storedSession.Value is null)
            {
                _portalUserSessionState.SetGuest();
                EnsureHeartbeatLoopStarted();
                return;
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == storedSession.Value.UserId, cancellationToken);

            if (user is null)
            {
                await _sessionStorage.DeleteAsync(SessionStorageKey);
                _portalUserSessionState.SetGuest();
                EnsureHeartbeatLoopStarted();
                return;
            }

            _portalUserSessionState.SetAuthenticated(new PortalSessionSnapshot(user.Id, user.UserName, user.DisplayName, user.Role));
            EnsureHeartbeatLoopStarted();
        }
        catch (InvalidOperationException)
        {
            _portalUserSessionState.SetGuest();
            EnsureHeartbeatLoopStarted();
        }
    }

    public async Task<PortalSignInResult> SignInAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var normalizedUserName = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
        {
            return new PortalSignInResult(false, "Vui lòng nhập đầy đủ tài khoản và mật khẩu.", null);
        }

        var passwordHash = ComputeSha256Upper(password);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserName == normalizedUserName && x.PasswordHash == passwordHash,
                cancellationToken);

        if (user is null)
        {
            return new PortalSignInResult(false, "Thông tin đăng nhập không hợp lệ.", null);
        }

        var snapshot = new PortalSessionSnapshot(user.Id, user.UserName, user.DisplayName, user.Role);
        await _sessionStorage.SetAsync(SessionStorageKey, snapshot);
        _portalUserSessionState.SetAuthenticated(snapshot);
        EnsureHeartbeatLoopStarted();
        await SendHeartbeatAsync(cancellationToken);

        return new PortalSignInResult(true, "Đăng nhập thành công.", user.Role);
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _sessionStorage.DeleteAsync(SessionStorageKey);
        }
        catch (InvalidOperationException)
        {
        }

        _portalUserSessionState.SetGuest();
        EnsureHeartbeatLoopStarted();
        await SendHeartbeatAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_heartbeatLoopCts is null)
        {
            return;
        }

        _heartbeatLoopCts.Cancel();
        if (_heartbeatLoopTask is not null)
        {
            try
            {
                await _heartbeatLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _heartbeatLoopCts.Dispose();
        _heartbeatLoopCts = null;
    }

    private void EnsureHeartbeatLoopStarted()
    {
        if (_heartbeatLoopCts is not null)
        {
            return;
        }

        _heartbeatLoopCts = new CancellationTokenSource();
        _heartbeatLoopTask = Task.Run(() => RunHeartbeatLoopAsync(_heartbeatLoopCts.Token));
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        await SendHeartbeatAsync(cancellationToken);

        using var timer = new PeriodicTimer(HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await SendHeartbeatAsync(cancellationToken);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _activeUserTrackerService.TrackWebHeartbeatAsync(_portalUserSessionState.UserId, _sessionKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Bỏ qua lỗi tạm thời để không ảnh hưởng trải nghiệm đăng nhập web.
        }
    }

    private static string ComputeSha256Upper(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
