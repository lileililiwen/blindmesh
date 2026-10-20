using Blindmesh.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Tasks;

/// <summary>
/// The tables of the participations. They are owned by the participations
/// package and are not changed by any other module.
/// </summary>
public static class ParticipationModelConfiguration
{
    public static void ApplyParticipationModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Participation>(entity =>
        {
            entity.ToTable("participations");
            entity.HasKey(participation => participation.Id);
            entity.Property(participation => participation.Id).HasColumnName("id");
            entity.Property(participation => participation.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(participation => participation.ApplicationId).HasColumnName("application_id").IsRequired();
            entity.Property(participation => participation.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(participation => participation.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(participation => participation.SlotId).HasColumnName("slot_id");
            entity.Property(participation => participation.LeaseId).HasColumnName("lease_id");
            entity.Property(participation => participation.AppliedAt).HasColumnName("applied_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(participation => participation.AcceptedAt).HasColumnName("accepted_at").HasColumnType("timestamptz");
            entity.Property(participation => participation.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");
            entity.Property(participation => participation.ExecutionDeadline).HasColumnName("execution_deadline").HasColumnType("timestamptz");
            entity.Property(participation => participation.EndedAt).HasColumnName("ended_at").HasColumnType("timestamptz");
            entity.Property(participation => participation.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(participation => participation.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(participation => participation.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            // One participation per task and contributor.
            entity.HasIndex(participation => new { participation.TaskId, participation.ContributorId })
                .IsUnique()
                .HasDatabaseName("ux_participations_task_id_contributor_id");
            entity.HasIndex(participation => participation.ApplicationId)
                .IsUnique()
                .HasDatabaseName("ux_participations_application_id");
            entity.HasIndex(participation => new { participation.ContributorId, participation.CreatedAt })
                .HasDatabaseName("ix_participations_contributor_id_created_at");
            entity.HasIndex(participation => new { participation.State, participation.AppliedAt })
                .HasDatabaseName("ix_participations_state_applied_at");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(participation => participation.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskApplication>(entity =>
        {
            entity.ToTable("applications");
            entity.HasKey(application => application.Id);
            entity.Property(application => application.Id).HasColumnName("id");
            entity.Property(application => application.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(application => application.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(application => application.CapabilitySnapshot).HasColumnName("capability_snapshot").HasMaxLength(4000).IsRequired();
            entity.Property(application => application.ScopeConsent).HasColumnName("scope_consent").IsRequired();
            entity.Property(application => application.ExpectedVersion).HasColumnName("expected_version").IsRequired();
            entity.Property(application => application.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            // One application per task and contributor.
            entity.HasIndex(application => new { application.TaskId, application.ContributorId })
                .IsUnique()
                .HasDatabaseName("ux_applications_task_id_contributor_id");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(application => application.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlotLease>(entity =>
        {
            entity.ToTable("slot_leases");
            entity.HasKey(lease => lease.Id);
            entity.Property(lease => lease.Id).HasColumnName("id");
            entity.Property(lease => lease.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(lease => lease.SlotId).HasColumnName("slot_id").IsRequired();
            entity.Property(lease => lease.ParticipationId).HasColumnName("participation_id").IsRequired();
            entity.Property(lease => lease.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(lease => lease.AcquiredAt).HasColumnName("acquired_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(lease => lease.ReleasedAt).HasColumnName("released_at").HasColumnType("timestamptz");

            // Only one live lease may exist for a slot, so two owners cannot
            // hold the same seat.
            entity.HasIndex(lease => lease.SlotId)
                .IsUnique()
                .HasFilter("released_at IS NULL")
                .HasDatabaseName("ux_slot_leases_slot_id_live");
            entity.HasIndex(lease => lease.ParticipationId)
                .IsUnique()
                .HasFilter("released_at IS NULL")
                .HasDatabaseName("ux_slot_leases_participation_id_live");
            entity.HasOne<MarketplaceTask>()
                .WithMany()
                .HasForeignKey(lease => lease.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipationStatusChange>(entity =>
        {
            entity.ToTable("participation_history");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id).HasColumnName("id");
            entity.Property(change => change.ParticipationId).HasColumnName("participation_id").IsRequired();
            entity.Property(change => change.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(change => change.FromStatus).HasColumnName("from_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(change => change.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(change => new { change.ParticipationId, change.ChangedAt })
                .HasDatabaseName("ix_participation_history_participation_id_changed_at");
            entity.HasOne<Participation>()
                .WithMany()
                .HasForeignKey(change => change.ParticipationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
