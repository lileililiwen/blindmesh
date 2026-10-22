using System.Net;
using System.Text.Json;
using Blindmesh.Tests.Findings;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The shared journey of the notification tests: a published task, a seat of
/// bob that runs, the evidence of the seat and the acceptance of the owner.
/// Every step goes through the real endpoints, so the event of the outbox is
/// written inside the transaction of the command of the product.
/// </summary>
internal sealed class NotificationScenario(NotificationTestHost host)
{
    /// <summary>The seat of the journey.</summary>
    public sealed record Seat(
        string ProjectId,
        string TaskId,
        string TaskTitle,
        string ParticipationId,
        string SubmissionId);

    /// <summary>
    /// Runs a seat of a published task until the owner accepts the work of the
    /// seat. The acceptance writes the event of the outbox, so the consumer of
    /// the product writes the notification of the contributor.
    /// </summary>
    public async Task<Seat> RunAcceptedSeatAsync(string stamp)
    {
        var seat = await RunSeatAsync(stamp);

        await host.SignInAsync("alice");

        var accepted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/submissions/{seat.SubmissionId}/accept",
            "{\"bonus\":0}",
            idempotencyKey: $"notif-accept-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        return seat;
    }

    /// <summary>Publishes a task, gives a seat to bob and lets him send the evidence.</summary>
    public async Task<Seat> RunSeatAsync(string stamp)
    {
        await host.SignInAsync("alice");
        await host.ClaimGrantAsync();

        var projectId = await host.CreateActiveProjectAsync($"Notification project {stamp}");
        var taskTitle = $"Notification seat {stamp}";

        var created = await host.SendAsync(
            HttpMethod.Post,
            "/v1/tasks",
            FindingsTestHost.TaskBody(
                projectId,
                title: taskTitle,
                requirements: ["{\"kind\":\"region\",\"value\":\"eu-central\"}"],
                steps: ["{\"title\":\"Open the task page\",\"expected_result\":\"The page shows the seat.\"}"],
                deadline: FindingsTestHost.DeadlineInDays()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var taskId = (await host.ReadJsonAsync(created)).RootElement.GetProperty("id").GetGuid();

        var published = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"notif-publish-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        var taskVersion = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Tasks.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => task.Version)
                .FirstAsync());

        await host.SignInAsync("bob");
        await host.OfferCapabilityAsync();

        var applied = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/applications",
            $"{{\"scope_consent\":true,\"expected_version\":{taskVersion}}}",
            idempotencyKey: $"notif-apply-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);

        var participationId = (await host.ReadJsonAsync(applied)).RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("alice");

        var accepted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/accept",
            idempotencyKey: $"notif-accept-seat-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await host.SignInAsync("bob");

        var started = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/start",
            idempotencyKey: $"notif-start-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var submitted = await host.SendAsync(
            HttpMethod.Post,
            $"/v1/participations/{participationId}/submissions",
            """
            {
              "summary":"I walked through the seat and the scope of the task is done.",
              "scope_completed":true,
              "steps":[{"position":1,"title":"Open the task page","result":"The page shows the seat of the task."}]
            }
            """,
            idempotencyKey: $"notif-submit-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var submissionId = (await host.ReadJsonAsync(submitted)).RootElement.GetProperty("id").GetGuid();

        return new Seat(projectId, taskId.ToString(), taskTitle, participationId.ToString(), submissionId.ToString());
    }

    /// <summary>
    /// Reads the notifications of the signed in contributor over the real
    /// endpoint of the API.
    /// </summary>
    public async Task<JsonDocument> ReadNotificationsAsync()
    {
        var response = await host.SendAsync(HttpMethod.Get, "/v1/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await host.ReadJsonAsync(response);
    }
}
