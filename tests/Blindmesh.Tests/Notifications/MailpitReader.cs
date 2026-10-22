using System.Text.Json;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The reader of the real mail sink of the test run. It reads the messages
/// back over the HTTP interface of the sink, so the test proves the real
/// transport and not a stand in of the answer of the mail service.
/// </summary>
internal sealed class MailpitReader
{
    /// <summary>The port of the SMTP interface of the mail sink.</summary>
    public const int SmtpPort = 51025;

    private const string BaseAddress = "http://127.0.0.1:58025";

    private readonly HttpClient _client = new() { BaseAddress = new Uri(BaseAddress), Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>True when the mail sink answers.</summary>
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync("/api/v1/messages", cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// The message of the sink that carries the given message identifier. Null
    /// when the sink holds no message with it.
    /// </summary>
    public async Task<MailpitMessage?> FindByMessageIdAsync(string messageId, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync("/api/v1/messages", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("messages", out var messages))
        {
            return null;
        }

        foreach (var message in messages.EnumerateArray())
        {
            if (!message.TryGetProperty("MessageID", out var candidate) ||
                !string.Equals(Normalise(candidate.GetString()), Normalise(messageId), StringComparison.Ordinal))
            {
                continue;
            }

            var id = message.TryGetProperty("ID", out var identifier) ? identifier.GetString() : null;

            if (id is null)
            {
                continue;
            }

            using var detail = await _client.GetAsync($"/api/v1/message/{id}", cancellationToken);
            detail.EnsureSuccessStatusCode();

            var detailBody = await detail.Content.ReadAsStringAsync(cancellationToken);

            using var detailDocument = JsonDocument.Parse(detailBody);

            return new MailpitMessage(
                detailDocument.RootElement.GetProperty("MessageID").GetString() ?? string.Empty,
                ReadAddress(detailDocument.RootElement, "From"),
                ReadRecipients(detailDocument.RootElement),
                detailDocument.RootElement.TryGetProperty("Subject", out var subject)
                    ? subject.GetString() ?? string.Empty
                    : string.Empty,
                detailDocument.RootElement.TryGetProperty("Text", out var text)
                    ? text.GetString() ?? string.Empty
                    : string.Empty);
        }

        return null;
    }

    /// <summary>
    /// The message identifier without the angle brackets. The sink strips them
    /// from the header of the message, so both sides are compared in the same
    /// shape.
    /// </summary>
    private static string Normalise(string? value) =>
        (value ?? string.Empty).Trim().Trim('<', '>');

    private static string ReadAddress(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) &&
        value.TryGetProperty("Address", out var address)
            ? address.GetString() ?? string.Empty
            : string.Empty;

    private static IReadOnlyList<string> ReadRecipients(JsonElement root)
    {
        if (!root.TryGetProperty("To", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var addresses = new List<string>();

        foreach (var recipient in recipients.EnumerateArray())
        {
            if (recipient.TryGetProperty("Address", out var address))
            {
                addresses.Add(address.GetString() ?? string.Empty);
            }
        }

        return addresses;
    }
}

/// <summary>One message of the real mail sink.</summary>
internal sealed record MailpitMessage(
    string MessageId,
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string Text);
