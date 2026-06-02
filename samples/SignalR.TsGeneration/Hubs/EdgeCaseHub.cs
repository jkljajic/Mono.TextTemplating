using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SignalR.TsGeneration.Hubs
{
    // DTOs for testing complex type mappings
    public record UserProfile (Guid Id, string Name, string? AvatarUrl, DateTime JoinedAt, bool IsAdmin);
    public record PagedResult<T> (List<T> Items, int Total, int Page, int PageSize);
    public enum MessagePriority { Low, Normal, High, Critical }

    public interface IEdgeCaseClient
    {
        Task OnDataChanged (UserProfile user, DateTimeOffset timestamp);
        Task OnError (string code, string? detail);
    }

    /// <summary>
    /// Edge-case hub covering complex types, generics, overloads, and filtering.
    /// Tests the T4 template's type-to-TypeScript mapping for all scenarios.
    /// </summary>
    public class EdgeCaseHub : Hub<IEdgeCaseClient>
    {
        // ── Primitives ──
        public Task<string> GetString () => Task.FromResult ("ok");
        public Task<bool> GetBool () => Task.FromResult (true);
        public Task<int> GetInt () => Task.FromResult (42);
        public Task<long> GetLong () => Task.FromResult (42L);
        public Task<double> GetDouble () => Task.FromResult (3.14);
        public Task<decimal> GetDecimal () => Task.FromResult (3.14m);
        public Task<float> GetFloat () => Task.FromResult (3.14f);
        public Task<byte> GetByte () => Task.FromResult ((byte) 255);

        // ── Special types ──
        public Task<Guid> CreateSession (string userId) => Task.FromResult (Guid.NewGuid ());
        public Task<DateTime> GetServerTime () => Task.FromResult (DateTime.UtcNow);
        public Task<DateTimeOffset> GetOffset () => Task.FromResult (DateTimeOffset.UtcNow);

        // ── Complex return types ──
        public Task<UserProfile> GetProfile (Guid userId) =>
            Task.FromResult (new UserProfile (userId, "Test", null, DateTime.UtcNow, false));
        public Task<List<UserProfile>> ListUsers (int pageSize) =>
            Task.FromResult (new List<UserProfile> ());
        public Task<PagedResult<UserProfile>> SearchUsers (string query, int page, int pageSize) =>
            Task.FromResult (new PagedResult<UserProfile> (new List<UserProfile> (), 0, page, pageSize));
        public Task<Dictionary<string, int>> GetStats () =>
            Task.FromResult (new Dictionary<string, int> { { "active", 5 }, { "total", 42 } });

        // ── Nullable parameters ──
        public Task UpdateAvatar (Guid userId, string? avatarUrl)
        {
            return Task.CompletedTask;
        }

        // ── Enums ──
        public Task SetPriority (Guid messageId, MessagePriority priority)
        {
            return Task.CompletedTask;
        }
        public Task<MessagePriority> GetPriority (Guid messageId) =>
            Task.FromResult (MessagePriority.Normal);

        // ── Arrays ──
        public Task ProcessBatch (string[] items, int[] ids, bool[] flags)
        {
            return Task.CompletedTask;
        }

        // ── Mixed nullable + value types ──
        public Task Configure (int? timeoutMs, bool? enabled, Guid? parentId)
        {
            return Task.CompletedTask;
        }

        // ── Methods that should be EXCLUDED from generated TS ──
        // CancellationToken → should be skipped (not a SignalR-serializable type)
        public Task DoWorkAsync (string jobId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        // Non-Task return type → should NOT appear in TS output
        public int GetConnectionCount () => 0;

        // Overloads → should both appear in TS output
        public Task Notify (string userId) => Task.CompletedTask;
        public Task Notify (string userId, MessagePriority priority) => Task.CompletedTask;

        // Void return → should NOT appear
        public void InternalHelper () { }

        // Static → should NOT appear
        public static string Version => "1.0";

        // Non-public → should NOT appear
        protected Task AdminOnly () => Task.CompletedTask;

        // Event (special name) → should NOT appear
        public event Action? OnDisconnected;
    }
}
