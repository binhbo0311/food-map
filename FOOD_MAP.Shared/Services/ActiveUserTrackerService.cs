using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading;

namespace FOOD_MAP.Shared.Services;

public sealed class ActiveUserTrackerService : IActiveUserTrackerService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(1);
    private static readonly SemaphoreSlim StorageBootstrapLock = new(1, 1);
    private static int _isStorageBootstrapped;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ActiveUserTrackerService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public Task TrackWebHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default)
    {
        return TrackHeartbeatAsync("web", userId, sessionKey, cancellationToken);
    }

    public Task TrackMobileHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default)
    {
        return TrackHeartbeatAsync("mobile", userId, sessionKey, cancellationToken);
    }

    public async Task<ActiveUserSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            await EnsureStorageReadyAsync(dbContext, cancellationToken);
        }
        catch (TimeoutException)
        {
        }
        catch (NpgsqlException)
        {
        }
        await PurgeExpiredAsync(dbContext, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var threshold = now - ActiveWindow;

        var activeRows = await dbContext.ActiveClientHeartbeats
            .AsNoTracking()
            .Where(x => x.LastSeenUtc >= threshold)
            .ToListAsync(cancellationToken);

        var totalActiveClients = activeRows.Count;
        var activeWebClients = activeRows.Count(x => string.Equals(x.ClientType, "web", StringComparison.OrdinalIgnoreCase));
        var activeMobileClients = activeRows.Count(x => string.Equals(x.ClientType, "mobile", StringComparison.OrdinalIgnoreCase));
        var activeAuthenticatedUsers = activeRows
            .Where(x => x.UserId.HasValue)
            .Select(x => x.UserId!.Value)
            .Distinct()
            .Count();

        return new ActiveUserSummary(totalActiveClients, activeWebClients, activeMobileClients, activeAuthenticatedUsers);
    }

    private async Task TrackHeartbeatAsync(string clientType, int? userId, string sessionKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            await PurgeExpiredAsync(dbContext, CancellationToken.None);

            var normalizedKey = sessionKey.Trim();
            var nowUtc = DateTimeOffset.UtcNow;
            var heartbeatId = Guid.NewGuid().ToString("N");

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "ActiveClientHeartbeats" ("Id", "ClientType", "UserId", "LastSeenUtc", "SessionKey")
                VALUES ({heartbeatId}, {clientType}, {userId}, {nowUtc}, {normalizedKey})
                ON CONFLICT ("SessionKey")
                DO UPDATE SET
                    "ClientType" = EXCLUDED."ClientType",
                    "UserId" = EXCLUDED."UserId",
                    "LastSeenUtc" = EXCLUDED."LastSeenUtc";
                """,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private static async Task EnsureStorageReadyAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _isStorageBootstrapped) == 1)
        {
            return;
        }

        await StorageBootstrapLock.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _isStorageBootstrapped) == 1)
            {
                return;
            }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS "ActiveClientHeartbeats" (
                        "Id" character varying(64) NOT NULL,
                        "ClientType" character varying(20) NOT NULL,
                        "UserId" integer NULL,
                        "LastSeenUtc" timestamp with time zone NOT NULL,
                        "SessionKey" character varying(120) NOT NULL,
                        CONSTRAINT "PK_ActiveClientHeartbeats" PRIMARY KEY ("Id")
                    );
                    """,
                    cancellationToken);

                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_ActiveClientHeartbeats_SessionKey"
                    ON "ActiveClientHeartbeats" ("SessionKey");
                    """,
                    cancellationToken);

                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    CREATE INDEX IF NOT EXISTS "IX_ActiveClientHeartbeats_ClientType_LastSeenUtc"
                    ON "ActiveClientHeartbeats" ("ClientType", "LastSeenUtc");
                    """,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                Volatile.Write(ref _isStorageBootstrapped, 1);
                return;
            }
            catch (NpgsqlException)
            {
                Volatile.Write(ref _isStorageBootstrapped, 1);
                return;
            }

            Volatile.Write(ref _isStorageBootstrapped, 1);
        }
        finally
        {
            StorageBootstrapLock.Release();
        }
    }

    private static async Task PurgeExpiredAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            var cutoffUtc = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10);
            var expiredRows = await dbContext.ActiveClientHeartbeats
                .Where(x => x.LastSeenUtc < cutoffUtc)
                .ToListAsync(CancellationToken.None);

            if (expiredRows.Count == 0)
            {
                return;
            }

            dbContext.ActiveClientHeartbeats.RemoveRange(expiredRows);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
}
