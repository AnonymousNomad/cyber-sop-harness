using System.Text;

namespace CyberSopHarness.Core;

public enum ProviderFailureClass
{
    None,
    Unavailable,
    Timeout,
    InvalidOutput,
    PolicyBlocked,
    Unknown
}

public enum ToolResultStatus
{
    Success,
    Partial,
    Timeout,
    Unknown,
    ToolError,
    Blocked
}

public enum Replayability
{
    Replayable,
    PartiallyReplayable,
    NonReplayable
}

public enum WorkflowState
{
    Ready,
    Planned,
    Proposed,
    Allowed,
    Running,
    Stopping,
    Observed,
    Verified,
    Blocked,
    Unknown,
    Stopped,
    Reportable
}

public enum FindingState
{
    Hypothesis,
    Candidate,
    Reproducible,
    Verified,
    Reportable,
    Unverified,
    Rejected,
    Unknown,
    Blocked
}

public sealed record ProviderDescriptor(
    string ProviderRef,
    string ModelRef,
    string ModelVersion,
    string ConfigurationHash,
    string ContextPolicy,
    string DataRetentionPolicy,
    string ToolCallMode);

public sealed record ProviderProposal(
    ProviderDescriptor Provider,
    ActionRequest Action,
    string OutputSha256,
    TimeSpan Latency,
    int TokenUsage,
    ProviderFailureClass FailureClass);

public sealed record ProviderExecutionMetadata(
    ProviderDescriptor Descriptor,
    string OutputSha256,
    TimeSpan Latency,
    int TokenUsage,
    ProviderFailureClass FailureClass);

public interface IModelProviderAdapter
{
    ProviderDescriptor Descriptor { get; }
    Task<ProviderProposal> ProposeAsync(string prompt, AuthorizationManifest manifest, CancellationToken cancellationToken);
}

public static class ActionRequestValidator
{
    public static ValidationResult Validate(ActionRequest? action)
    {
        if (action is null) return new(false, new[] { "action request is required" });
        var errors = new List<string>();
        if (action.Type != "ACTION_REQUEST") errors.Add("action type is invalid");
        if (string.IsNullOrWhiteSpace(action.RunId) || string.IsNullOrWhiteSpace(action.ActionId) || string.IsNullOrWhiteSpace(action.Phase) || string.IsNullOrWhiteSpace(action.TargetRef) || string.IsNullOrWhiteSpace(action.CapabilityRef) || string.IsNullOrWhiteSpace(action.Purpose) || string.IsNullOrWhiteSpace(action.ScopeRef) || string.IsNullOrWhiteSpace(action.AuthorizationRef)) errors.Add("action identity or purpose is incomplete");
        if (action.Arguments is null || action.MethodologyRefs is null || action.ResolvedAddresses is null || action.MethodologyRefs.Count == 0 || action.MethodologyRefs.Any(string.IsNullOrWhiteSpace)) errors.Add("action collections are invalid");
        if (!Enum.IsDefined(action.RiskClass)) errors.Add("action risk is invalid");
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }
}

public static class ProviderProposalValidator
{
    public static ValidationResult Validate(ProviderProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var errors = new List<string>();
        if (proposal.Provider is null) return new(false, new[] { "provider descriptor is required" });
        var provider = proposal.Provider;
        if (string.IsNullOrWhiteSpace(provider.ProviderRef)) errors.Add("provider reference is required");
        if (string.IsNullOrWhiteSpace(provider.ModelRef)) errors.Add("model reference is required");
        if (string.IsNullOrWhiteSpace(provider.ModelVersion)) errors.Add("model version is required");
        if (provider.ConfigurationHash is null || !IsSha256(provider.ConfigurationHash)) errors.Add("provider configuration hash is invalid");
        if (string.IsNullOrWhiteSpace(provider.ContextPolicy)) errors.Add("context policy is required");
        if (string.IsNullOrWhiteSpace(provider.DataRetentionPolicy)) errors.Add("data retention policy is required");
        if (string.IsNullOrWhiteSpace(provider.ToolCallMode)) errors.Add("tool-call mode is required");
        if (proposal.Latency < TimeSpan.Zero || proposal.TokenUsage < 0) errors.Add("provider measurements are invalid");
        if (proposal.OutputSha256 is null || !IsSha256(proposal.OutputSha256)) errors.Add("provider output hash is invalid");
        if (proposal.FailureClass == ProviderFailureClass.None)
        {
            var actionValidation = ActionRequestValidator.Validate(proposal.Action);
            if (!actionValidation.IsValid) errors.AddRange(actionValidation.Errors);
        }
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }

    internal static bool IsSha256(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
}

public sealed record ActionEnvelope(
    string EnvelopeId,
    ActionRequest Request,
    ProviderDescriptor Provider,
    string ProviderOutputSha256,
    DateTimeOffset CreatedAt,
    TimeSpan ProviderLatency,
    int ProviderTokenUsage,
    ProviderFailureClass ProviderFailureClass)
{
    public string ActionHash => Canonicalization.ActionHash(Request);
}

public static class ActionEnvelopeFactory
{
    public static ActionEnvelope Create(ProviderProposal proposal)
    {
        var validation = ProviderProposalValidator.Validate(proposal);
        if (!validation.IsValid) throw new InvalidOperationException("provider proposal is invalid: " + string.Join("; ", validation.Errors));
        if (proposal.FailureClass != ProviderFailureClass.None) throw new InvalidOperationException("failed provider proposals cannot become action envelopes");
        return new("envelope_" + Guid.NewGuid().ToString("N"), proposal.Action, proposal.Provider, proposal.OutputSha256, AuthoritativeClock.UtcNow, proposal.Latency, proposal.TokenUsage, proposal.FailureClass);
    }
}

public static class ActionEnvelopeValidator
{
    public static ValidationResult Validate(ActionEnvelope? envelope)
    {
        if (envelope is null) return new(false, new[] { "action envelope is required" });
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(envelope.EnvelopeId)) errors.Add("envelope identity is required");
        var action = ActionRequestValidator.Validate(envelope.Request);
        if (!action.IsValid) errors.AddRange(action.Errors);
        var proposal = new ProviderProposal(envelope.Provider, envelope.Request, envelope.ProviderOutputSha256, envelope.ProviderLatency, envelope.ProviderTokenUsage, envelope.ProviderFailureClass);
        var provider = ProviderProposalValidator.Validate(proposal);
        if (!provider.IsValid) errors.AddRange(provider.Errors);
        if (envelope.ProviderFailureClass != ProviderFailureClass.None) errors.Add("failed provider output cannot be dispatched");
        if (envelope.CreatedAt > AuthoritativeClock.UtcNow.AddSeconds(5)) errors.Add("envelope creation time is in the future");
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }
}

public sealed record ToolCapabilityManifest(
    string ToolRef,
    string ToolVersion,
    string CapabilityRef,
    string RequiredPrivilege,
    bool ReadOnly,
    IReadOnlyList<string> NetworkDestinations,
    IReadOnlyList<string> DataClasses,
    bool RequiresContainedWorker,
    IReadOnlyList<string> EvidenceRequirements,
    bool CleanupRequired,
    TimeSpan MaxDuration,
    long MaxOutputBytes);

public sealed record ToolRegistration(ToolCapabilityManifest Manifest, IToolAdapter Adapter);

internal interface ILocalFixtureToolAdapter : IToolAdapter
{
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolRegistration> _registrations = new(StringComparer.Ordinal);
    private bool _frozen;

    public bool IsFrozen => _frozen;

    public void Register(ToolCapabilityManifest manifest, IToolAdapter adapter)
    {
        if (_frozen) throw new InvalidOperationException("tool registry is frozen");
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(adapter);
        if (string.IsNullOrWhiteSpace(manifest.ToolRef) || string.IsNullOrWhiteSpace(manifest.ToolVersion) || string.IsNullOrWhiteSpace(manifest.CapabilityRef)) throw new ArgumentException("tool identity is incomplete", nameof(manifest));
        if (!manifest.RequiresContainedWorker || !manifest.ReadOnly || manifest.NetworkDestinations.Count != 0 || !manifest.CleanupRequired || manifest.MaxDuration <= TimeSpan.Zero || manifest.MaxOutputBytes <= 0) throw new InvalidOperationException("Phase 3 tools must be read-only, contained, bounded, network-disabled, and cleanup-required");
        if (manifest.DataClasses.Any(dataClass => dataClass is not "synthetic" and not "none")) throw new InvalidOperationException("Phase 3 tools may only handle synthetic or no data");
        if (!manifest.EvidenceRequirements.Contains("raw", StringComparer.Ordinal) || !manifest.EvidenceRequirements.Contains("redacted", StringComparer.Ordinal) || !manifest.EvidenceRequirements.Contains("observation", StringComparer.Ordinal)) throw new InvalidOperationException("tool evidence requirements are incomplete");
        if (adapter is not ILocalFixtureToolAdapter) throw new InvalidOperationException("Phase 3 accepts only registered local fixture adapters");
        if (!string.Equals(adapter.ToolRef, manifest.ToolRef, StringComparison.Ordinal) || !string.Equals(adapter.ToolVersion, manifest.ToolVersion, StringComparison.Ordinal)) throw new InvalidOperationException("adapter identity does not match tool manifest");
        var frozenManifest = manifest with
        {
            NetworkDestinations = manifest.NetworkDestinations.ToArray().AsReadOnly(),
            DataClasses = manifest.DataClasses.ToArray().AsReadOnly(),
            EvidenceRequirements = manifest.EvidenceRequirements.ToArray().AsReadOnly()
        };
        if (!_registrations.TryAdd(frozenManifest.CapabilityRef, new ToolRegistration(frozenManifest, adapter))) throw new InvalidOperationException("tool capability is already registered");
    }

    public void Freeze() => _frozen = true;

    public bool TryGet(string capabilityRef, out ToolRegistration? registration) => _registrations.TryGetValue(capabilityRef, out registration);
}

public sealed record ToolExecutionContext(
    ActionEnvelope Envelope,
    AuthorizationManifest Manifest,
    PolicyResult Policy,
    Permit Permit,
    ToolCapabilityManifest Capability,
    string WorkerRef);

public sealed class SyntheticFixtureToolAdapter : ILocalFixtureToolAdapter
{
    private readonly byte[] _raw;
    private readonly ToolResultStatus _status;
    private readonly string _observation;

    public SyntheticFixtureToolAdapter(string toolRef, string toolVersion, string raw, ToolResultStatus status, string observation)
    {
        ToolRef = toolRef;
        ToolVersion = toolVersion;
        _raw = Encoding.UTF8.GetBytes(raw);
        _status = status;
        _observation = observation;
    }

    public string ToolRef { get; }
    public string ToolVersion { get; }

    public Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ToolAdapterResult(_status, 0, _raw, new[] { _observation }, Array.Empty<string>(), "PENDING"));
    }

    public Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("CLEANUP_OK|" + context.Envelope.ActionHash);
    }
}

public sealed record ToolAdapterResult(
    ToolResultStatus Status,
    int? ExitCode,
    byte[] RawOutput,
    IReadOnlyList<string> ObservationRefs,
    IReadOnlyList<string> ArtifactRefs,
    string CleanupResult);

public interface IToolAdapter
{
    string ToolRef { get; }
    string ToolVersion { get; }
    Task<ToolAdapterResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken);
    Task<string> CleanupAsync(ToolExecutionContext context, ToolAdapterResult result, CancellationToken cancellationToken);
}
