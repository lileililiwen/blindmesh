using Blindmesh.Domain.Findings;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Findings;

/// <summary>
/// The tables of the findings module. They are owned by the findings package
/// and are not changed by any other module.
/// </summary>
public static class FindingsModelConfiguration
{
    public static void ApplyFindingsModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.ToTable("submissions");
            entity.HasKey(submission => submission.Id);
            entity.Property(submission => submission.Id).HasColumnName("id");
            entity.Property(submission => submission.ParticipationId).HasColumnName("participation_id").IsRequired();
            entity.Property(submission => submission.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(submission => submission.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(submission => submission.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(submission => submission.OwnerId).HasColumnName("owner_id").IsRequired();
            entity.Property(submission => submission.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(submission => submission.Summary).HasColumnName("summary").HasMaxLength(5000).IsRequired();
            entity.Property(submission => submission.ScopeCompleted).HasColumnName("scope_completed").IsRequired();
            entity.Property(submission => submission.RevisionNumber).HasColumnName("revision_number").IsRequired();
            entity.Property(submission => submission.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamptz");
            entity.Property(submission => submission.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamptz");
            entity.Property(submission => submission.CorrectionRequestedAt).HasColumnName("correction_requested_at").HasColumnType("timestamptz");
            entity.Property(submission => submission.DisputeReason).HasColumnName("dispute_reason").HasMaxLength(5000);
            entity.Property(submission => submission.CorrectionDeadline).HasColumnName("correction_deadline").HasColumnType("timestamptz");
            entity.Property(submission => submission.BonusGranted).HasColumnName("bonus_granted").IsRequired();
            entity.Property(submission => submission.SettledCredits).HasColumnName("settled_credits").IsRequired();
            entity.Property(submission => submission.AutoAccepted).HasColumnName("auto_accepted").IsRequired();
            entity.Property(submission => submission.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(submission => submission.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(submission => submission.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            // One submission per seat.
            entity.HasIndex(submission => submission.ParticipationId)
                .IsUnique()
                .HasDatabaseName("ux_submissions_participation_id");
            entity.HasIndex(submission => new { submission.ContributorId, submission.CreatedAt })
                .HasDatabaseName("ix_submissions_contributor_id_created_at");
            entity.HasIndex(submission => new { submission.State, submission.SubmittedAt })
                .HasDatabaseName("ix_submissions_state_submitted_at");
            entity.HasOne<Blindmesh.Domain.Tasks.MarketplaceTask>()
                .WithMany()
                .HasForeignKey(submission => submission.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubmissionRevision>(entity =>
        {
            entity.ToTable("submission_revisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.Id).HasColumnName("id");
            entity.Property(revision => revision.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(revision => revision.Number).HasColumnName("number").IsRequired();
            entity.Property(revision => revision.Summary).HasColumnName("summary").HasMaxLength(5000).IsRequired();
            entity.Property(revision => revision.ScopeCompleted).HasColumnName("scope_completed").IsRequired();
            entity.Property(revision => revision.StepResults).HasColumnName("step_results").HasColumnType("jsonb").IsRequired();
            entity.Property(revision => revision.TestedAreas).HasColumnName("tested_areas").HasColumnType("jsonb").IsRequired();
            entity.Property(revision => revision.CleanResultExplanation).HasColumnName("clean_result_explanation").HasMaxLength(5000).IsRequired();
            entity.Property(revision => revision.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            // One revision of one number per submission.
            entity.HasIndex(revision => new { revision.SubmissionId, revision.Number })
                .IsUnique()
                .HasDatabaseName("ux_submission_revisions_submission_id_number");
            entity.HasOne<Submission>()
                .WithMany()
                .HasForeignKey(revision => revision.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubmissionStatusChange>(entity =>
        {
            entity.ToTable("submission_history");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id).HasColumnName("id");
            entity.Property(change => change.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(change => change.FromStatus).HasColumnName("from_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired();
            entity.Property(change => change.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(change => change.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(change => new { change.SubmissionId, change.ChangedAt })
                .HasDatabaseName("ix_submission_history_submission_id_changed_at");
            entity.HasOne<Submission>()
                .WithMany()
                .HasForeignKey(change => change.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.ToTable("findings");
            entity.HasKey(finding => finding.Id);
            entity.Property(finding => finding.Id).HasColumnName("id");
            entity.Property(finding => finding.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(finding => finding.RevisionId).HasColumnName("revision_id").IsRequired();
            entity.Property(finding => finding.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(finding => finding.Category)
                .HasColumnName("category")
                .HasMaxLength(20)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(finding => finding.Severity)
                .HasColumnName("severity")
                .HasMaxLength(10)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(finding => finding.Title).HasColumnName("title").HasMaxLength(140).IsRequired();
            entity.Property(finding => finding.Reproduction).HasColumnName("reproduction").HasMaxLength(10000).IsRequired();
            entity.Property(finding => finding.ExpectedResult).HasColumnName("expected_result").HasMaxLength(5000).IsRequired();
            entity.Property(finding => finding.ActualResult).HasColumnName("actual_result").HasMaxLength(5000).IsRequired();
            entity.Property(finding => finding.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(finding => finding.DecisionNote).HasColumnName("decision_note").HasMaxLength(1000);
            entity.Property(finding => finding.DecidedBy).HasColumnName("decided_by");
            entity.Property(finding => finding.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamptz");
            entity.Property(finding => finding.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(finding => finding.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(finding => finding.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(finding => new { finding.SubmissionId, finding.CreatedAt })
                .HasDatabaseName("ix_findings_submission_id_created_at");
            entity.HasIndex(finding => new { finding.State, finding.Severity })
                .HasDatabaseName("ix_findings_state_severity");
            entity.HasOne<Submission>()
                .WithMany()
                .HasForeignKey(finding => finding.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FindingHistoryEntry>(entity =>
        {
            entity.ToTable("finding_history");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.FindingId).HasColumnName("finding_id").IsRequired();
            entity.Property(entry => entry.FromStatus).HasColumnName("from_status").HasMaxLength(16).IsRequired();
            entity.Property(entry => entry.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired();
            entity.Property(entry => entry.ChangedBy).HasColumnName("changed_by").IsRequired();
            entity.Property(entry => entry.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
            entity.HasIndex(entry => new { entry.FindingId, entry.ChangedAt })
                .HasDatabaseName("ix_finding_history_finding_id_changed_at");
            entity.HasOne<Finding>()
                .WithMany()
                .HasForeignKey(entry => entry.FindingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("attachments");
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.Id).HasColumnName("id");
            entity.Property(attachment => attachment.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(attachment => attachment.RevisionId).HasColumnName("revision_id").IsRequired();
            entity.Property(attachment => attachment.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(attachment => attachment.FileName).HasColumnName("file_name").HasMaxLength(200).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
            entity.Property(attachment => attachment.SizeBytes).HasColumnName("size_bytes").IsRequired();
            entity.Property(attachment => attachment.StorageKey).HasColumnName("storage_key").HasMaxLength(400).IsRequired();
            entity.Property(attachment => attachment.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(attachment => attachment.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(300);
            entity.Property(attachment => attachment.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
            entity.Property(attachment => attachment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(attachment => attachment.ScannedAt).HasColumnName("scanned_at").HasColumnType("timestamptz");

            entity.HasIndex(attachment => attachment.RevisionId)
                .HasDatabaseName("ix_attachments_revision_id");
            entity.HasIndex(attachment => new { attachment.State, attachment.CreatedAt })
                .HasDatabaseName("ix_attachments_state_created_at");
            entity.HasOne<Submission>()
                .WithMany()
                .HasForeignKey(attachment => attachment.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
