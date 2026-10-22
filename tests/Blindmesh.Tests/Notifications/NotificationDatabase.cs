using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The reset of the database of the test run. Every test of the package starts
/// with an empty database, so the notifications of a test never influence the
/// result of another test.
/// </summary>
internal static class NotificationDatabase
{
    public static async Task ResetAsync(RuntimeFixture fixture)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE notification_deliveries, notification_preferences, reputation_snapshots, reputation_scores, reputation_events, outbox_messages, account_bans, dispute_evidence, moderation_audit, moderator_assignments, disputes, reports, attachments, finding_history, findings, submission_history, submission_revisions, submissions, participation_history, slot_leases, applications, participations, task_slots, task_steps, task_requirements, task_history, tasks, ledger_entries, ledger_transactions, ledger_accounts, credit_lots, grant_claims, idempotency_records, project_members, project_versions, project_status_history, projects, capabilities, capability_tags, capability_status_history CASCADE");

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE users SET role = CASE username WHEN 'dana' THEN 'moderator' WHEN 'erin' THEN 'administrator' ELSE 'user' END");
    }
}
