using Blindmesh.Domain.Moderation;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Moderation;

/// <summary>
/// The tables of the moderation module. They are owned by the moderation
/// package and are not changed by any other module.
/// </summary>
public static class ModerationModelConfiguration
{
    public static void ApplyModerationModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("reports");
            entity.HasKey(report => report.Id);
            entity.Property(report => report.Id).HasColumnName("id");
            entity.Property(report => report.ReporterId).HasColumnName("reporter_id").IsRequired();
            entity.Property(report => report.Receipt).HasColumnName("receipt").HasMaxLength(64).IsRequired();
            entity.Property(report => report.Reason).HasColumnName("reason").HasMaxLength(5000).IsRequired();
            entity.Property(report => report.SubjectType).HasColumnName("subject_type").HasMaxLength(40).IsRequired();
            entity.Property(report => report.SubjectId).HasColumnName("subject_id");
            entity.Property(report => report.State)
                .HasColumnName("state")
                .HasMaxLength(16)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(report => report.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(report => report.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamptz");
            entity.Property(report => report.Resolution).HasColumnName("resolution").HasMaxLength(2000);
            entity.Property(report => report.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(report => report.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(report => report.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            // The receipt of a report is unique, so a reporter reads a stable
            // code back.
            entity.HasIndex(report => report.Receipt)
                .IsUnique()
                .HasDatabaseName("ux_reports_receipt");
            entity.HasIndex(report => new { report.ReporterId, report.CreatedAt })
                .HasDatabaseName("ix_reports_reporter_id_created_at");
            entity.HasIndex(report => new { report.State, report.CreatedAt })
                .HasDatabaseName("ix_reports_state_created_at");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("disputes");
            entity.HasKey(dispute => dispute.Id);
            entity.Property(dispute => dispute.Id).HasColumnName("id");
            entity.Property(dispute => dispute.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(dispute => dispute.ParticipationId).HasColumnName("participation_id").IsRequired();
            entity.Property(dispute => dispute.TaskId).HasColumnName("task_id").IsRequired();
            entity.Property(dispute => dispute.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(dispute => dispute.PublisherId).HasColumnName("publisher_id").IsRequired();
            entity.Property(dispute => dispute.ContributorId).HasColumnName("contributor_id").IsRequired();
            entity.Property(dispute => dispute.ReportId).HasColumnName("report_id");
            entity.Property(dispute => dispute.State)
                .HasColumnName("state")
                .HasMaxLength(20)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(dispute => dispute.Reason).HasColumnName("reason").HasMaxLength(5000).IsRequired();
            entity.Property(dispute => dispute.BaseReward).HasColumnName("base_reward").IsRequired();
            entity.Property(dispute => dispute.AwardedAmount).HasColumnName("awarded_amount").HasDefaultValue(0L).IsRequired();
            entity.Property(dispute => dispute.AwardToContributor).HasColumnName("award_to_contributor");
            entity.Property(dispute => dispute.EvidenceNote).HasColumnName("evidence_note").HasMaxLength(2000);
            entity.Property(dispute => dispute.ResolutionReason).HasColumnName("resolution_reason").HasMaxLength(2000);
            entity.Property(dispute => dispute.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(dispute => dispute.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");
            entity.Property(dispute => dispute.LedgerTransactionId).HasColumnName("ledger_transaction_id");
            entity.Property(dispute => dispute.LedgerTransactionKind).HasColumnName("ledger_transaction_kind").HasMaxLength(20);
            entity.Property(dispute => dispute.AppealedBy).HasColumnName("appealed_by");
            entity.Property(dispute => dispute.AppealedAt).HasColumnName("appealed_at").HasColumnType("timestamptz");
            entity.Property(dispute => dispute.AppealReason).HasColumnName("appeal_reason").HasMaxLength(2000);
            entity.Property(dispute => dispute.ClosedBy).HasColumnName("closed_by");
            entity.Property(dispute => dispute.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamptz");
            entity.Property(dispute => dispute.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(dispute => dispute.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(dispute => dispute.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(dispute => new { dispute.SubmissionId, dispute.State })
                .HasDatabaseName("ix_disputes_submission_id_state");
            entity.HasIndex(dispute => new { dispute.State, dispute.CreatedAt })
                .HasDatabaseName("ix_disputes_state_created_at");
            entity.HasIndex(dispute => dispute.ReportId)
                .HasDatabaseName("ix_disputes_report_id");
        });

        modelBuilder.Entity<AccountBan>(entity =>
        {
            entity.ToTable("account_bans");
            entity.HasKey(ban => ban.Id);
            entity.Property(ban => ban.Id).HasColumnName("id");
            entity.Property(ban => ban.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(ban => ban.BannedUntil).HasColumnName("banned_until").HasColumnType("timestamptz");
            entity.Property(ban => ban.BanReason).HasColumnName("ban_reason").HasColumnType("text");
            entity.Property(ban => ban.BannedBy).HasColumnName("banned_by");
            entity.Property(ban => ban.BannedAt).HasColumnName("banned_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(ban => ban.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(ban => ban.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(ban => ban.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            // One ban per account. A second ban of the same account overwrites
            // the row of the first one.
            entity.HasIndex(ban => ban.UserId)
                .IsUnique()
                .HasDatabaseName("ux_account_bans_user_id");
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.ToTable("dispute_evidence");
            entity.HasKey(evidence => evidence.Id);
            entity.Property(evidence => evidence.Id).HasColumnName("id");
            entity.Property(evidence => evidence.DisputeId).HasColumnName("dispute_id").IsRequired();
            entity.Property(evidence => evidence.SubmittedBy).HasColumnName("submitted_by").IsRequired();
            entity.Property(evidence => evidence.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
            entity.Property(evidence => evidence.Note).HasColumnName("note").HasMaxLength(5000).IsRequired();
            entity.Property(evidence => evidence.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(evidence => new { evidence.DisputeId, evidence.CreatedAt })
                .HasDatabaseName("ix_dispute_evidence_dispute_id_created_at");
            entity.HasOne<Dispute>()
                .WithMany()
                .HasForeignKey(evidence => evidence.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModeratorAssignment>(entity =>
        {
            entity.ToTable("moderator_assignments");
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.Id).HasColumnName("id");
            entity.Property(assignment => assignment.DisputeId).HasColumnName("dispute_id").IsRequired();
            entity.Property(assignment => assignment.ModeratorId).HasColumnName("moderator_id").IsRequired();
            entity.Property(assignment => assignment.Role).HasColumnName("role").HasMaxLength(20).IsRequired();
            entity.Property(assignment => assignment.AssignedBy).HasColumnName("assigned_by");
            entity.Property(assignment => assignment.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(assignment => assignment.ReleasedAt).HasColumnName("released_at").HasColumnType("timestamptz");

            entity.HasIndex(assignment => new { assignment.DisputeId, assignment.AssignedAt })
                .HasDatabaseName("ix_moderator_assignments_dispute_id_assigned_at");
            entity.HasIndex(assignment => new { assignment.DisputeId, assignment.ModeratorId })
                .HasDatabaseName("ix_moderator_assignments_dispute_id_moderator_id");
            entity.HasOne<Dispute>()
                .WithMany()
                .HasForeignKey(assignment => assignment.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModerationAudit>(entity =>
        {
            entity.ToTable("moderation_audit");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Id).HasColumnName("id");
            entity.Property(audit => audit.DisputeId).HasColumnName("dispute_id");
            entity.Property(audit => audit.ReportId).HasColumnName("report_id");
            entity.Property(audit => audit.ActorId).HasColumnName("actor_id").IsRequired();
            entity.Property(audit => audit.Action).HasColumnName("action").HasMaxLength(60).IsRequired();
            entity.Property(audit => audit.Reason).HasColumnName("reason").HasMaxLength(5000).IsRequired();
            entity.Property(audit => audit.Details).HasColumnName("details").HasMaxLength(2000);
            entity.Property(audit => audit.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(audit => new { audit.DisputeId, audit.CreatedAt })
                .HasDatabaseName("ix_moderation_audit_dispute_id_created_at");
            entity.HasIndex(audit => new { audit.ReportId, audit.CreatedAt })
                .HasDatabaseName("ix_moderation_audit_report_id_created_at");
        });
    }
}
