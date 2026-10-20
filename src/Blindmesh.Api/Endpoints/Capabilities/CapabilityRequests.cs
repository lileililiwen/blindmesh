namespace Blindmesh.Api.Endpoints.Capabilities;

/// <summary>The body of a create command of a capability.</summary>
public sealed record CreateCapabilityRequest(
    string? Kind,
    string? Label,
    string[]? Tags,
    string? Visibility,
    bool? Available,
    Dictionary<string, string>? Metadata);

/// <summary>
/// The body of a change of a capability. A field that the caller left out keeps
/// its current value.
/// </summary>
public sealed record UpdateCapabilityRequest(
    int? ExpectedVersion,
    string? Kind,
    string? Label,
    string[]? Tags,
    string? Visibility,
    bool? Available,
    Dictionary<string, string>? Metadata);

/// <summary>The body of a withdraw command of a capability.</summary>
public sealed record WithdrawCapabilityRequest(int? ExpectedVersion);

/// <summary>One hard condition of an eligibility evaluation.</summary>
public sealed record EligibilityConditionRequest(string? Kind, string? Value);

/// <summary>The body of an eligibility evaluation with a set of hard conditions.</summary>
public sealed record EvaluateEligibilityRequest(EligibilityConditionRequest[]? Conditions);
