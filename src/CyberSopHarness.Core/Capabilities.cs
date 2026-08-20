namespace CyberSopHarness.Core;

public sealed record CapabilityManifest(
    string CapabilityRef,
    RiskClass RiskClass,
    IReadOnlyList<string> AllowedTargetRefs,
    string RequiredPrivilege,
    bool ReadOnly,
    IReadOnlyList<string> NetworkDestinations,
    IReadOnlyList<string> DataClasses,
    TimeSpan MaxDuration,
    long MaxOutputBytes,
    bool RequiresApproval,
    bool RequiresContainedWorker);

public sealed class CapabilityRegistry
{
    private readonly Dictionary<string, CapabilityManifest> _capabilities = new(StringComparer.Ordinal);
    private bool _frozen;

    public void Register(CapabilityManifest capability)
    {
        if (_frozen) throw new InvalidOperationException("capability registry is frozen");
        if (string.IsNullOrWhiteSpace(capability.CapabilityRef)) throw new ArgumentException("capability reference is required", nameof(capability));
        if (capability.MaxDuration <= TimeSpan.Zero || capability.MaxOutputBytes <= 0) throw new ArgumentException("capability limits must be positive", nameof(capability));
        if (!Enum.IsDefined(capability.RiskClass)) throw new ArgumentException("capability risk is invalid", nameof(capability));
        var frozen = capability with
        {
            AllowedTargetRefs = capability.AllowedTargetRefs.ToArray().AsReadOnly(),
            NetworkDestinations = capability.NetworkDestinations.ToArray().AsReadOnly(),
            DataClasses = capability.DataClasses.ToArray().AsReadOnly()
        };
        if (!_capabilities.TryAdd(frozen.CapabilityRef, frozen)) throw new InvalidOperationException("capability already registered");
    }

    public void Freeze()
    {
        _frozen = true;
    }

    public bool IsFrozen => _frozen;

    public bool TryGet(string capabilityRef, out CapabilityManifest? capability) => _capabilities.TryGetValue(capabilityRef, out capability);

    public ValidationResult Validate(ActionRequest request, AuthorizationManifest manifest)
    {
        if (!TryGet(request.CapabilityRef, out var capability) || capability is null) return new(false, new[] { "capability is not registered" });
        var errors = new List<string>();
        if (capability.RiskClass != request.RiskClass) errors.Add("action risk does not match capability risk");
        if (!capability.AllowedTargetRefs.Contains("*", StringComparer.Ordinal) && !capability.AllowedTargetRefs.Contains(request.TargetRef, StringComparer.OrdinalIgnoreCase)) errors.Add("capability target is not allowlisted");
        if (capability.RequiresApproval && string.IsNullOrWhiteSpace(request.ApprovalRef)) errors.Add("capability requires approval");
        if (!capability.RequiresContainedWorker) errors.Add("capability is not approved for contained execution");
        if (request.CredentialRef is not null && !manifest.CredentialPolicy.AllowedRefs.Contains(request.CredentialRef, StringComparer.Ordinal)) errors.Add("credential handle is not allowed by the manifest");
        if (manifest.DataHandling.Classification == "synthetic-only" && capability.DataClasses.Any(dataClass => dataClass is not "synthetic" and not "none")) errors.Add("capability data class exceeds synthetic-only manifest");
        return errors.Count == 0 ? ValidationResult.Valid() : new(false, errors);
    }
}
