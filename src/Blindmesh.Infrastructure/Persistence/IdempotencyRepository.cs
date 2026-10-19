using Blindmesh.Application.Abstractions;
using Blindmesh.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>Stores command outcomes for idempotent replay.</summary>
public sealed class IdempotencyRepository(BlindmeshDbContext dbContext) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> FindAsync(string scope, string key, CancellationToken cancellationToken)
    {
        var entity = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.Scope == scope && record.Key == key,
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new IdempotencyRecord
        {
            Scope = entity.Scope,
            Key = entity.Key,
            PayloadHash = entity.PayloadHash,
            StatusCode = entity.StatusCode,
            ResponseBody = entity.ResponseBody,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
        };
    }

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        dbContext.IdempotencyRecords.Add(new IdempotencyRecordEntity
        {
            Id = Guid.NewGuid(),
            Scope = record.Scope,
            Key = record.Key,
            PayloadHash = record.PayloadHash,
            StatusCode = record.StatusCode,
            ResponseBody = record.ResponseBody,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
        });

        await Task.CompletedTask;
    }
}
