using System.Text;

namespace Blindmesh.Domain.Findings;

/// <summary>
/// The rules of a private file of the evidence. A file is text or a log of at
/// most two mebibytes or a picture of at most ten mebibytes, and a revision
/// carries at most ten files. An executable, an SVG, an HTML file and an
/// archive are refused, and the kind of the file is checked against the
/// signature of its bytes before it becomes readable.
/// </summary>
public static class AttachmentRules
{
    /// <summary>The number of files one revision may carry.</summary>
    public const int MaxAttachmentsPerRevision = 10;

    /// <summary>The largest text or log of one file.</summary>
    public const long TextMaxBytes = 2 * 1024 * 1024;

    /// <summary>The largest picture of one file.</summary>
    public const long ImageMaxBytes = 10 * 1024 * 1024;

    private static readonly (string Extension, string ContentType)[] TextFiles =
    [
        (".txt", "text/plain"),
        (".log", "text/plain"),
        (".md", "text/markdown"),
        (".csv", "text/csv"),
        (".json", "application/json"),
    ];

    private static readonly (string Extension, string ContentType)[] ImageFiles =
    [
        (".png", "image/png"),
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".webp", "image/webp"),
    ];

    /// <summary>The allowed extensions of a text or a log.</summary>
    public static IReadOnlyList<string> TextExtensions { get; } =
        TextFiles.Select(file => file.Extension).ToArray();

    /// <summary>The allowed extensions of a picture.</summary>
    public static IReadOnlyList<string> ImageExtensions { get; } =
        ImageFiles.Select(file => file.Extension).ToArray();

    /// <summary>True when the extension of the file is allowed.</summary>
    public static bool IsAllowedExtension(string extension)
    {
        var normalized = extension.ToLowerInvariant();

        return TextFiles.Any(file => file.Extension == normalized) ||
            ImageFiles.Any(file => file.Extension == normalized);
    }

    /// <summary>The content type the extension of the file has to declare.</summary>
    public static string? ContentTypeFor(string extension)
    {
        var normalized = extension.ToLowerInvariant();

        return TextFiles.Concat(ImageFiles)
            .FirstOrDefault(file => file.Extension == normalized)
            .ContentType;
    }

    /// <summary>True when the content type of the file is allowed.</summary>
    public static bool IsAllowedContentType(string contentType)
    {
        var normalized = contentType.ToLowerInvariant();

        return TextFiles.Any(file => file.ContentType == normalized) ||
            ImageFiles.Any(file => file.ContentType == normalized);
    }

    /// <summary>
    /// The largest number of bytes the kind of the file accepts. Null when the
    /// kind is not allowed.
    /// </summary>
    public static long? MaxBytesFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "text/plain" or "text/markdown" or "text/csv" or "application/json" => TextMaxBytes,
        "image/png" or "image/jpeg" or "image/webp" => ImageMaxBytes,
        _ => null,
    };

    /// <summary>
    /// The content type the signature of the bytes of the file proves, or null
    /// when the bytes match no allowed kind. A file whose bytes hold an
    /// executable, an archive, an SVG or an HTML page never matches.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (IsRefusedSignature(bytes))
        {
            return null;
        }

        if (HasPngSignature(bytes))
        {
            return "image/png";
        }

        if (HasJpegSignature(bytes))
        {
            return "image/jpeg";
        }

        if (HasWebpSignature(bytes))
        {
            return "image/webp";
        }

        if (HasTextSignature(bytes))
        {
            return "text/plain";
        }

        return null;
    }

    /// <summary>
    /// True when the bytes of the file hold the marker of the standard test
    /// file of the antivirus programs. The marker is refused, so a test of the
    /// pipeline proves that the pipeline refuses it.
    /// </summary>
    public static bool HoldsTestMarker(ReadOnlySpan<byte> bytes) =>
        Encoding.ASCII.GetString(bytes).Contains(
            "EICAR-STANDARD-ANTIVIRUS-TEST-FILE",
            StringComparison.Ordinal);

    /// <summary>
    /// True when the bytes of the file hold the signature of an executable, of
    /// an archive, of an SVG or of an HTML page.
    /// </summary>
    public static bool IsRefusedSignature(ReadOnlySpan<byte> bytes)
    {
        if (StartsWith(bytes, "MZ") || StartsWith(bytes, "\u007fELF") || StartsWith(bytes, "#!"))
        {
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B &&
            (bytes[2] == 0x03 || bytes[2] == 0x05 || bytes[2] == 0x07))
        {
            return true;
        }

        if (StartsWith(bytes, "\u0000asm") || StartsWith(bytes, "dex\n"))
        {
            return true;
        }

        var head = Encoding.ASCII.GetString(bytes[..Math.Min(bytes.Length, 512)]).TrimStart().ToLowerInvariant();

        return head.StartsWith("<svg", StringComparison.Ordinal) ||
            head.StartsWith("<?xml", StringComparison.Ordinal) && head.Contains("<svg", StringComparison.Ordinal) ||
            head.StartsWith("<!doctype html", StringComparison.Ordinal) ||
            head.StartsWith("<html", StringComparison.Ordinal);
    }

    private static bool HasPngSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool HasJpegSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool HasWebpSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 &&
        bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
        bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

    private static bool HasTextSignature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        if (bytes.IndexOf((byte)0) >= 0)
        {
            return false;
        }

        try
        {
            var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

            strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        return true;
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, string marker) =>
        bytes.Length >= marker.Length &&
        Encoding.ASCII.GetString(bytes[..marker.Length]) == marker;
}
