using System.Globalization;
using System.Text;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The position of a page in the wallet history. The cursor carries the time
/// and the id of the last transaction of the page, so the next page is read in
/// the same stable descending order.
/// </summary>
public sealed record CreditCursor(DateTimeOffset CreatedAt, Guid Id)
{
    /// <summary>Encodes the cursor into the opaque value of the list envelope.</summary>
    public string Encode()
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{CreatedAt.UtcDateTime.Ticks}:{Id:D}");

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Decodes the cursor of a list request. Null means that the value is not
    /// a cursor of this product, so the caller answers with a bad request.
    /// </summary>
    public static CreditCursor? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var padded = value.Replace('-', '+').Replace('_', '/');

        padded += (value.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        string raw;

        try
        {
            raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            return null;
        }

        var separator = raw.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0)
        {
            return null;
        }

        if (!long.TryParse(raw.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return null;
        }

        if (!Guid.TryParse(raw.AsSpan(separator + 1), out var id))
        {
            return null;
        }

        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        return new CreditCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }
}
