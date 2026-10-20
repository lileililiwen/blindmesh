using Blindmesh.Domain.Capabilities;
using Blindmesh.Domain.Credits;
using Blindmesh.Domain.Identity;
using Blindmesh.Domain.Projects;
using Blindmesh.Infrastructure.Capabilities;
using Blindmesh.Infrastructure.Credits;
using Blindmesh.Infrastructure.Findings;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Infrastructure.Persistence.Entities;
using Blindmesh.Infrastructure.Projects;
using Blindmesh.Infrastructure.Tasks;
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

    public DbSet<User> Users => Set<User>();

    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    public DbSet<UserSession> Sessions => Set<UserSession>();

    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();

    public DbSet<ProjectStatusChange> ProjectStatusChanges => Set<ProjectStatusChange>();

    public DbSet<Capability> Capabilities => Set<Capability>();

    public DbSet<CapabilityTag> CapabilityTags => Set<CapabilityTag>();

    public DbSet<CapabilityStatusChange> CapabilityStatusChanges => Set<CapabilityStatusChange>();

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<CreditLot> CreditLots => Set<CreditLot>();

    public DbSet<GrantClaim> GrantClaims => Set<GrantClaim>();

    public DbSet<Blindmesh.Domain.Tasks.MarketplaceTask> Tasks => Set<Blindmesh.Domain.Tasks.MarketplaceTask>();

    public DbSet<Blindmesh.Domain.Tasks.TaskRequirement> TaskRequirements => Set<Blindmesh.Domain.Tasks.TaskRequirement>();

    public DbSet<Blindmesh.Domain.Tasks.TaskStep> TaskSteps => Set<Blindmesh.Domain.Tasks.TaskStep>();

    public DbSet<Blindmesh.Domain.Tasks.TaskSlot> TaskSlots => Set<Blindmesh.Domain.Tasks.TaskSlot>();

    public DbSet<Blindmesh.Domain.Tasks.TaskStatusChange> TaskStatusChanges => Set<Blindmesh.Domain.Tasks.TaskStatusChange>();

    public DbSet<Blindmesh.Domain.Tasks.Participation> Participations => Set<Blindmesh.Domain.Tasks.Participation>();

    public DbSet<Blindmesh.Domain.Tasks.TaskApplication> TaskApplications => Set<Blindmesh.Domain.Tasks.TaskApplication>();

    public DbSet<Blindmesh.Domain.Tasks.SlotLease> SlotLeases => Set<Blindmesh.Domain.Tasks.SlotLease>();

    public DbSet<Blindmesh.Domain.Tasks.ParticipationStatusChange> ParticipationStatusChanges => Set<Blindmesh.Domain.Tasks.ParticipationStatusChange>();

    public DbSet<Blindmesh.Domain.Findings.Submission> Submissions => Set<Blindmesh.Domain.Findings.Submission>();

    public DbSet<Blindmesh.Domain.Findings.SubmissionRevision> SubmissionRevisions => Set<Blindmesh.Domain.Findings.SubmissionRevision>();

    public DbSet<Blindmesh.Domain.Findings.SubmissionStatusChange> SubmissionHistory => Set<Blindmesh.Domain.Findings.SubmissionStatusChange>();

    public DbSet<Blindmesh.Domain.Findings.Finding> Findings => Set<Blindmesh.Domain.Findings.Finding>();

    public DbSet<Blindmesh.Domain.Findings.FindingHistoryEntry> FindingHistory => Set<Blindmesh.Domain.Findings.FindingHistoryEntry>();

    public DbSet<Blindmesh.Domain.Findings.Attachment> Attachments => Set<Blindmesh.Domain.Findings.Attachment>();

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

        // The tables of the identity module. They are owned by the identity
        // package and are not changed by any other module.
        modelBuilder.ApplyIdentityModel();
        modelBuilder.ApplyProjectModel();
        modelBuilder.ApplyCapabilityModel();

        // The tables of the credits module. They are owned by the credits
        // package and are not changed by any other module.
        modelBuilder.ApplyCreditModel();

        // The tables of the tasks module. They are owned by the tasks package
        // and are not changed by any other module.
        modelBuilder.ApplyTaskModel();
        modelBuilder.ApplyParticipationModel();

        // The tables of the findings module. They are owned by the findings
        // package and are not changed by any other module.
        modelBuilder.ApplyFindingsModel();
    }
}
