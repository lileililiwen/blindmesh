using Blindmesh.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// The tables of the operations module. They are owned by the operations
/// package and they are not changed by any other module.
/// </summary>
public static class OperationsModelConfiguration
{
    public static void ApplyOperationsModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.ToTable("operations_backups");
            entity.HasKey(backup => backup.Id);
            entity.Property(backup => backup.Id).HasColumnName("id");
            entity.Property(backup => backup.TakenAt).HasColumnName("taken_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(backup => backup.Kind)
                .HasColumnName("kind")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(backup => backup.Location).HasColumnName("location").HasMaxLength(200).IsRequired();
            entity.Property(backup => backup.SizeBytes).HasColumnName("size_bytes").IsRequired();
            entity.Property(backup => backup.ChecksumSha256).HasColumnName("checksum_sha256").HasMaxLength(64).IsRequired();
            entity.Property(backup => backup.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(backup => backup.RetentionUntil).HasColumnName("retention_until").HasColumnType("timestamptz").IsRequired();
            entity.Property(backup => backup.Detail).HasColumnName("detail").HasMaxLength(500).IsRequired();

            entity.HasIndex(backup => backup.TakenAt)
                .HasDatabaseName("ix_operations_backups_taken_at");
            entity.HasIndex(backup => backup.ChecksumSha256)
                .HasDatabaseName("ix_operations_backups_checksum_sha256");
        });

        modelBuilder.Entity<RestoreRehearsal>(entity =>
        {
            entity.ToTable("operations_restore_rehearsals");
            entity.HasKey(rehearsal => rehearsal.Id);
            entity.Property(rehearsal => rehearsal.Id).HasColumnName("id");
            entity.Property(rehearsal => rehearsal.BackupId).HasColumnName("backup_id");
            entity.Property(rehearsal => rehearsal.Namespace).HasColumnName("namespace").HasMaxLength(80).IsRequired();
            entity.Property(rehearsal => rehearsal.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(rehearsal => rehearsal.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");
            entity.Property(rehearsal => rehearsal.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(rehearsal => rehearsal.SchemaCheck).HasColumnName("schema_check").HasMaxLength(300).IsRequired();
            entity.Property(rehearsal => rehearsal.DataCheck).HasColumnName("data_check").HasMaxLength(300).IsRequired();
            entity.Property(rehearsal => rehearsal.ObjectCheck).HasColumnName("object_check").HasMaxLength(300).IsRequired();
            entity.Property(rehearsal => rehearsal.LedgerCheck).HasColumnName("ledger_check").HasMaxLength(300).IsRequired();
            entity.Property(rehearsal => rehearsal.Notes).HasColumnName("notes").HasMaxLength(500).IsRequired();
            entity.Property(rehearsal => rehearsal.BrowserJourneys).HasColumnName("browser_journeys");
            entity.Property(rehearsal => rehearsal.AxeRuns).HasColumnName("axe_runs");

            entity.HasIndex(rehearsal => rehearsal.StartedAt)
                .HasDatabaseName("ix_operations_restore_rehearsals_started_at");
            entity.HasIndex(rehearsal => rehearsal.BackupId)
                .HasDatabaseName("ix_operations_restore_rehearsals_backup_id");
        });

        modelBuilder.Entity<DeletionRequest>(entity =>
        {
            entity.ToTable("operations_deletion_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id).HasColumnName("id");
            entity.Property(request => request.RequesterId).HasColumnName("requester_id").IsRequired();
            entity.Property(request => request.SubjectUserId).HasColumnName("subject_user_id").IsRequired();
            entity.Property(request => request.Scope).HasColumnName("scope").HasMaxLength(200).IsRequired();
            entity.Property(request => request.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(request => request.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(request => request.PseudonymizedUserId).HasColumnName("pseudonymized_user_id").HasMaxLength(64).IsRequired();
            entity.Property(request => request.AuditReference).HasColumnName("audit_reference").HasMaxLength(120).IsRequired();

            entity.HasIndex(request => request.ReceivedAt)
                .HasDatabaseName("ix_operations_deletion_requests_received_at");
            entity.HasIndex(request => request.SubjectUserId)
                .HasDatabaseName("ix_operations_deletion_requests_subject_user_id");
        });
    }
}
