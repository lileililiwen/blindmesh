using Blindmesh.Domain.Reputation;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Reputation;

/// <summary>
/// The tables of the reputation module. They are owned by the reputation
/// package and are not changed by any other module.
/// </summary>
public static class ReputationModelConfiguration
{
    public static void ApplyReputationModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReputationEvent>(entity =>
        {
            entity.ToTable("reputation_events");
            entity.HasKey(reputationEvent => reputationEvent.Id);
            entity.Property(reputationEvent => reputationEvent.Id).HasColumnName("id");
            entity.Property(reputationEvent => reputationEvent.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(reputationEvent => reputationEvent.SourceEventId).HasColumnName("source_event_id").IsRequired();
            entity.Property(reputationEvent => reputationEvent.Category)
                .HasColumnName("category")
                .HasMaxLength(32)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(reputationEvent => reputationEvent.Delta).HasColumnName("delta").IsRequired();
            entity.Property(reputationEvent => reputationEvent.SourceParticipationId).HasColumnName("source_participation_id");
            entity.Property(reputationEvent => reputationEvent.SourceSubmissionId).HasColumnName("source_submission_id");
            entity.Property(reputationEvent => reputationEvent.SourceFindingId).HasColumnName("source_finding_id");
            entity.Property(reputationEvent => reputationEvent.SourceProjectId).HasColumnName("source_project_id");
            entity.Property(reputationEvent => reputationEvent.SourceTaskId).HasColumnName("source_task_id");
            entity.Property(reputationEvent => reputationEvent.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(reputationEvent => reputationEvent.Note).HasColumnName("note").HasMaxLength(200).IsRequired();

            // The message of the outbox is unique, so a message that is
            // delivered twice never counts twice.
            entity.HasIndex(reputationEvent => reputationEvent.SourceEventId)
                .IsUnique()
                .HasDatabaseName("ux_reputation_events_source_event_id");
            entity.HasIndex(reputationEvent => new { reputationEvent.UserId, reputationEvent.OccurredAt })
                .HasDatabaseName("ix_reputation_events_user_id_occurred_at");
        });

        modelBuilder.Entity<ReputationScore>(entity =>
        {
            entity.ToTable("reputation_scores");
            entity.HasKey(score => score.UserId);
            entity.Property(score => score.UserId).HasColumnName("user_id");
            entity.Property(score => score.AcceptedContributions).HasColumnName("accepted_contributions").IsRequired();
            entity.Property(score => score.ConfirmedFindings).HasColumnName("confirmed_findings").IsRequired();
            entity.Property(score => score.ReversedContributions).HasColumnName("reversed_contributions").IsRequired();
            entity.Property(score => score.CurrentScore).HasColumnName("current_score").IsRequired();
            entity.Property(score => score.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
        });

        modelBuilder.Entity<ReputationSnapshot>(entity =>
        {
            entity.ToTable("reputation_snapshots");
            entity.HasKey(snapshot => snapshot.Id);
            entity.Property(snapshot => snapshot.Id).HasColumnName("id");
            entity.Property(snapshot => snapshot.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(snapshot => snapshot.CurrentScore).HasColumnName("current_score").IsRequired();
            entity.Property(snapshot => snapshot.AcceptedContributions).HasColumnName("accepted_contributions").IsRequired();
            entity.Property(snapshot => snapshot.ConfirmedFindings).HasColumnName("confirmed_findings").IsRequired();
            entity.Property(snapshot => snapshot.ReversedContributions).HasColumnName("reversed_contributions").IsRequired();
            entity.Property(snapshot => snapshot.TakenAt).HasColumnName("taken_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(snapshot => new { snapshot.UserId, snapshot.TakenAt })
                .HasDatabaseName("ix_reputation_snapshots_user_id_taken_at");
        });
    }
}
