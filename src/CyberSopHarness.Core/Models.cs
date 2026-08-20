namespace CyberSopHarness.Core;

public enum EngagementMode
{
    Fixture,
    Authorized
}

public enum RiskClass
{
    R0,
    R1,
    R2,
    R3,
    R4
}

public enum PolicyDecision
{
    Allow,
    Block,
    ApprovalRequired
}

public enum PermitConsumptionState
{
    Unused,
    Consumed,
    Revoked,
    Expired
}

public sealed record TimeWindow(
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string TimeZone,
    IReadOnlyList<ExcludedWindow> ExcludedWindows);

public sealed record ExcludedWindow(DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, string Reason);

public sealed record ScopeDefinition(
    IReadOnlyList<string> Allow,
    IReadOnlyList<string> Deny,
    string WildcardPolicy,
    string RedirectPolicy,
    string ThirdPartyPolicy);

public sealed record MethodDefinition(IReadOnlyList<string> Allowed, IReadOnlyList<string> Prohibited);

public sealed record AssetCriticalityDefinition(
    string Default,
    IReadOnlyDictionary<string, string> Targets);

public sealed record DataHandlingDefinition(
    string Classification,
    string Redaction,
    string Retention);

public sealed record CredentialPolicy(
    IReadOnlyList<string> AllowedRefs,
    bool AutomaticUse,
    string ExpiryPolicy);

public sealed record RateLimitDefinition(
    double RequestsPerSecond,
    int Concurrency,
    long PayloadBytes);

public sealed record CleanupDefinition(
    bool Required,
    string Owner,
    string ProcedureRef);

public sealed record AuthorizationProof(
    string Owner,
    string Operator,
    string ArtifactRef,
    string SignatureBase64,
    string PublicKeyPem,
    string SignedPayload);

public sealed record EscalationContact(string Name, string Channel, string Address);

public sealed record AuthorizationManifest
{
    public string ManifestVersion { get; init; } = "1.0";
    public string EngagementId { get; init; } = string.Empty;
    public EngagementMode EngagementMode { get; init; }
    public AuthorizationProof Authorization { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    public IReadOnlyList<string> ThirdPartyRefs { get; init; } = Array.Empty<string>();
    public ScopeDefinition Scope { get; init; } = new(Array.Empty<string>(), Array.Empty<string>(), "single-level", "block", "block");
    public TimeWindow TimeWindow { get; init; } = new(DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue, "UTC", Array.Empty<ExcludedWindow>());
    public MethodDefinition Methods { get; init; } = new(Array.Empty<string>(), Array.Empty<string>());
    public AssetCriticalityDefinition AssetCriticality { get; init; } = new("unknown", new Dictionary<string, string>());
    public DataHandlingDefinition DataHandling { get; init; } = new("synthetic-only", "required", "phase");
    public IReadOnlyList<EscalationContact> EscalationContacts { get; init; } = Array.Empty<EscalationContact>();
    public CredentialPolicy CredentialPolicy { get; init; } = new(Array.Empty<string>(), false, "short-lived");
    public RateLimitDefinition RateLimits { get; init; } = new(0, 0, 0);
    public CleanupDefinition Cleanup { get; init; } = new(true, string.Empty, string.Empty);
    public IReadOnlyList<string> StopConditions { get; init; } = Array.Empty<string>();
}

public sealed record ActionRequest
{
    public string Type { get; init; } = "ACTION_REQUEST";
    public string RunId { get; init; } = string.Empty;
    public string ActionId { get; init; } = string.Empty;
    public string? ParentEventId { get; init; }
    public string Phase { get; init; } = string.Empty;
    public string TargetRef { get; init; } = string.Empty;
    public string CapabilityRef { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>();
    public string Purpose { get; init; } = string.Empty;
    public string? Hypothesis { get; init; }
    public string? ExpectedObservation { get; init; }
    public RiskClass RiskClass { get; init; }
    public string ScopeRef { get; init; } = string.Empty;
    public string AuthorizationRef { get; init; } = string.Empty;
    public IReadOnlyList<string> MethodologyRefs { get; init; } = Array.Empty<string>();
    public string? ApprovalRef { get; init; }
    public string? CredentialRef { get; init; }
    public IReadOnlyList<string> ResolvedAddresses { get; init; } = Array.Empty<string>();
}

public sealed record ApprovalRecord(
    string ApprovalRef,
    string RunId,
    string ActionId,
    string ActionHash,
    string ManifestHash,
    string TargetRef,
    string CapabilityRef,
    RiskClass RiskClass,
    string ApproverRef,
    DateTimeOffset ExpiresAt,
    string Rationale,
    string Nonce,
    string SignatureBase64 = "",
    string SignedPayload = "");

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Valid() => new(true, Array.Empty<string>());
}

public sealed record ScopeDecision(bool Allowed, string CanonicalTarget, string Reason);

public sealed record PolicyResult(
    PolicyDecision Decision,
    string PolicyRef,
    string PolicyVersion,
    string Reason,
    string? ScopeRef,
    string ActionHash,
    string ManifestHash,
    string ScopeHash,
    string AuthorizationRef,
    string CapabilityRef,
    RiskClass RiskClass,
    IReadOnlyList<string> MethodologyRefs);

public sealed class Permit
{
    public required string PermitId { get; init; }
    public required string RunId { get; init; }
    public required string ActionId { get; init; }
    public required string ActionHash { get; init; }
    public required string ManifestHash { get; init; }
    public required string CanonicalizationRef { get; init; }
    public required string TargetRef { get; init; }
    public required string ScopeRef { get; init; }
    public required string ScopeHash { get; init; }
    public required string PolicyRef { get; init; }
    public required string PolicyVersion { get; init; }
    public required string WorkerRef { get; init; }
    public required string CapabilityRef { get; init; }
    public required string AuthorizationRef { get; init; }
    public string? CredentialRef { get; init; }
    public required string ApprovalRef { get; init; }
    public required string ApprovalHash { get; init; }
    public required RiskClass RiskClass { get; init; }
    public required IReadOnlyList<string> MethodologyRefs { get; init; }
    public required string IssuerRef { get; init; }
    public required string IssuerSignatureBase64 { get; set; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Nonce { get; init; }
    public PermitConsumptionState ConsumptionState { get; internal set; } = PermitConsumptionState.Unused;
    public DateTimeOffset? ConsumedAt { get; internal set; }
}

public sealed record CredentialHandle(string Handle, DateTimeOffset ExpiresAt);

public sealed record WorkerResult(string Status, string ArtifactRef, string RawSha256, long OutputBytes);

public sealed record ContainmentAttestation(string WorkerRef, string ProviderRef, string BoundaryHash, bool ExternalEnforcement, string Mode, string PrivilegeLevel, bool HardStopGuaranteed, string SignatureBase64);

public sealed record RollbackReport(IReadOnlyList<string> Completed, IReadOnlyList<string> Failed);
