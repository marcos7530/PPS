using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Audit;

/// <summary>
/// EF Core SaveChanges interceptor that materializes enqueued audit drafts
/// into <see cref="AuditLog"/> entries within the same transaction.
/// Also implements <see cref="IAuditWriter"/> to serve as the enqueue endpoint.
/// If the audit INSERT fails, the entire operation is rolled back (Req 1.1, 1.8).
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor, IAuditWriter
{
    private readonly IClock _clock;
    private readonly IAuditContextAccessor _contextAccessor;
    private readonly List<AuditEntryDraft> _queue = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditSaveChangesInterceptor(IClock clock, IAuditContextAccessor contextAccessor)
    {
        _clock = clock;
        _contextAccessor = contextAccessor;
    }

    // ─── IAuditWriter ────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Enqueue(AuditEntryDraft draft)
    {
        _queue.Add(draft);
    }

    /// <inheritdoc />
    public async Task WriteFailedAttemptAsync(ErrorCode code, AuditContext ctx, CancellationToken ct)
    {
        // Standalone audit entry for a failed validation attempt (Req 1.2).
        // Uses the same DbContext but outside the main operation's SaveChanges flow.
        // The caller is responsible for ensuring this is invoked outside the main UoW transaction
        // or within a separate mini-transaction for the failure record.
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAt = _clock.UtcNow,
            UserId = ctx.UserId,
            UsernameSnapshot = ctx.UsernameSnapshot,
            OperationType = "FailedAttempt",
            EntityType = ctx.EntityType,
            EntityId = ctx.EntityId,
            RelatedEntityIds = null,
            Outcome = "failure",
            ErrorCode = code.ToString(),
            BeforeState = null,
            AfterState = null,
            Metadata = ctx.Metadata,
            SessionId = ctx.SessionId,
            IpAddress = ctx.IpAddress
        };

        // We need access to a DbContext. The interceptor is registered per-context,
        // so we store a reference when SavingChangesAsync fires. For WriteFailedAttemptAsync,
        // the caller must provide the context via the overload or we use a captured reference.
        // Since this interceptor is scoped and registered with the DbContext, we capture
        // the context reference on the first interception call and reuse it here.
        if (_capturedContext is null)
            throw new InvalidOperationException(
                "Cannot write failed attempt: no DbContext has been captured. " +
                "Ensure the interceptor is registered with the DbContext.");

        _capturedContext.Set<AuditLog>().Add(entry);
        await _capturedContext.SaveChangesAsync(ct);
    }

    // ─── Interceptor overrides ───────────────────────────────────────────

    private DbContext? _capturedContext;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return new ValueTask<InterceptionResult<int>>(result);

        _capturedContext = eventData.Context;

        MaterializeDrafts(eventData.Context);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return result;

        _capturedContext = eventData.Context;

        MaterializeDrafts(eventData.Context);

        return result;
    }

    // ─── Private helpers ─────────────────────────────────────────────────

    private void MaterializeDrafts(DbContext context)
    {
        if (_queue.Count == 0)
            return;

        var now = _clock.UtcNow;

        // Derive before/after JSON from ChangeTracker for entities being modified.
        // This enriches drafts that don't already carry explicit before/after state.
        var changeTrackerSnapshots = BuildChangeTrackerSnapshots(context);

        foreach (var draft in _queue)
        {
            var beforeState = draft.BeforeState;
            var afterState = draft.AfterState;

            // If the draft doesn't carry explicit state, try to derive from ChangeTracker
            if (beforeState is null && afterState is null && draft.EntityId.HasValue)
            {
                var key = (draft.EntityType, draft.EntityId.Value);
                if (changeTrackerSnapshots.TryGetValue(key, out var snapshot))
                {
                    beforeState = snapshot.Before;
                    afterState = snapshot.After;
                }
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAt = now,
                UserId = _contextAccessor.UserId,
                UsernameSnapshot = _contextAccessor.UsernameSnapshot,
                OperationType = draft.OperationType,
                EntityType = draft.EntityType,
                EntityId = draft.EntityId,
                RelatedEntityIds = draft.RelatedEntityIds is { Count: > 0 }
                    ? JsonSerializer.Serialize(draft.RelatedEntityIds, JsonOptions)
                    : null,
                Outcome = "success",
                ErrorCode = null,
                BeforeState = beforeState,
                AfterState = afterState,
                Metadata = draft.Metadata,
                SessionId = _contextAccessor.SessionId,
                IpAddress = _contextAccessor.IpAddress
            };

            context.Set<AuditLog>().Add(auditLog);
        }

        _queue.Clear();
    }

    private static Dictionary<(string EntityType, Guid EntityId), (string? Before, string? After)>
        BuildChangeTrackerSnapshots(DbContext context)
    {
        var snapshots = new Dictionary<(string, Guid), (string?, string?)>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted or EntityState.Added))
                continue;

            // Skip AuditLog entries themselves
            if (entry.Entity is AuditLog)
                continue;

            var entityType = entry.Metadata.ClrType.Name;
            var primaryKey = GetPrimaryKeyValue(entry);
            if (primaryKey is null)
                continue;

            string? before = null;
            string? after = null;

            switch (entry.State)
            {
                case EntityState.Modified:
                    before = SerializeValues(entry.OriginalValues);
                    after = SerializeValues(entry.CurrentValues);
                    break;

                case EntityState.Deleted:
                    before = SerializeValues(entry.OriginalValues);
                    after = null;
                    break;

                case EntityState.Added:
                    before = null;
                    after = SerializeValues(entry.CurrentValues);
                    break;
            }

            snapshots[(entityType, primaryKey.Value)] = (before, after);
        }

        return snapshots;
    }

    private static Guid? GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
            return null;

        // Only handle single Guid primary keys
        if (keyProperties.Count == 1)
        {
            var value = entry.CurrentValues[keyProperties[0]];
            if (value is Guid guid)
                return guid;
        }

        return null;
    }

    private static string SerializeValues(PropertyValues values)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in values.Properties)
        {
            var value = values[prop];
            // Skip navigation properties and row version
            if (prop.IsShadowProperty() || prop.ClrType == typeof(byte[]))
                continue;

            dict[prop.Name] = value;
        }

        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
