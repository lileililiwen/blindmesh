namespace Blindmesh.Domain.Operations;

/// <summary>
/// One item of the pilot gate. The item is a computed view and it is never a
/// table: it names the check, the value that was really measured, the value
/// that is expected, whether the check passed and the exact command that
/// produced the measurement.
///
/// A target that has no observation window yet is reported as
/// "target, not yet measured" and it never counts as passed.
/// </summary>
public sealed record PilotGateItem(
    string Name,
    string Measured,
    string Expected,
    bool Passed,
    string Command);
