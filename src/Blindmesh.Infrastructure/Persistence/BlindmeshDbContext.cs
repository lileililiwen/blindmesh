using Blindmesh.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context of the product. Infrastructure owns the migration
/// stream; no other project creates or applies migrations.
/// </summary>
public sealed class BlindmeshDbContext(DbContextOptions<BlindmeshDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).HasColumnName("id");
            entity.Property(message => message.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
            entity.Property(message => message.Version).HasColumnName("version").IsRequired();
            entity.Property(message => message.ActorId).HasColumnName("actor_id");
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
            entity.Property(message => message.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamptz");
            entity.Property(message => message.Attempts).HasColumnName("attempts").IsRequired();
            entity.Property(message => message.LastError).HasColumnName("last_error");
            entity.HasIndex(message => new { message.ProcessedAt, message.OccurredAt }).HasDatabaseName("ix_outbox_messages_pending");
        });

        modelBuilder.Entity<IdempotencyRecordEntity>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Scope).HasColumnName("scope").HasMaxLength(200).IsRequired();
            entity.Property(record => record.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
            entity.Property(record => record.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(record => record.StatusCode).HasColumnName("status_code").IsRequired();
            entity.Property(record => record.ResponseBody).HasColumnName("response_body").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(record => record.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(record => new { record.Scope, record.Key })
                .IsUnique()
                .HasDatabaseName("ux_idempotency_records_scope_key");
        });
    }
}
