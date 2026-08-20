using System.Text;

namespace CyberSopHarness.Core;

public enum ProviderEgressStatus
{
    Offline,
    Local,
    External
}

public sealed record ProviderDisclosure(
    string ProviderId,
    string ProviderName,
    string ModelRef,
    string ModelVersion,
    string Source,
    string LicenseStatus,
    string DataPath,
    string RetentionWarning,
    string ResourceEstimate,
    ProviderEgressStatus EgressStatus);

public sealed record ProviderSelectionEvent(
    string SelectionId,
    string ProviderRef,
    string ModelRef,
    ModelProviderKind Kind,
    ProviderEgressStatus EgressStatus,
    string? PreviousSelectionId,
    DateTimeOffset SelectedAt);

public static class ProviderDisclosureRenderer
{
    public static string Render(ProviderDisclosure disclosure)
    {
        ArgumentNullException.ThrowIfNull(disclosure);
        return string.Join(Environment.NewLine,
            "PROVIDER DISCLOSURE",
            "PROVIDER: " + disclosure.ProviderName + " (" + disclosure.ProviderId + ")",
            "MODEL: " + disclosure.ModelRef + " " + disclosure.ModelVersion,
            "SOURCE: " + disclosure.Source,
            "LICENSE: " + disclosure.LicenseStatus,
            "DATA PATH: " + disclosure.DataPath,
            "RETENTION: " + disclosure.RetentionWarning,
            "RESOURCE: " + disclosure.ResourceEstimate,
            "EGRESS: " + disclosure.EgressStatus.ToString().ToUpperInvariant());
    }
}

public sealed class ModelProviderWizard
{
    private readonly ModelProviderSelectionStore _store;
    private readonly PersistentSecretStore? _secrets;
    private readonly IReadOnlyList<ProviderDisclosure> _disclosures;

    public ModelProviderWizard(ModelProviderSelectionStore store, PersistentSecretStore? secrets, IReadOnlyList<ProviderDisclosure> disclosures)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _secrets = secrets;
        _disclosures = disclosures ?? throw new ArgumentNullException(nameof(disclosures));
    }

    public IReadOnlyList<ProviderDisclosure> Choices => _disclosures;

    public async Task<ProviderSelectionEvent> ConfirmAsync(string providerId, bool egressAcknowledged, string? previousSelectionId, CancellationToken cancellationToken)
    {
        var disclosure = _disclosures.FirstOrDefault(item => string.Equals(item.ProviderId, providerId, StringComparison.Ordinal))
            ?? throw new ArgumentException("provider choice is not available", nameof(providerId));

        if (disclosure.EgressStatus == ProviderEgressStatus.External && !egressAcknowledged) throw new InvalidOperationException("external egress must be explicitly acknowledged");
        if (disclosure.EgressStatus == ProviderEgressStatus.External && (_secrets is null || !_secrets.Exists(disclosure.ProviderId))) throw new InvalidOperationException("external provider has no stored secret; no silent fallback is permitted");

        var kind = disclosure.EgressStatus switch
        {
            ProviderEgressStatus.Offline => ModelProviderKind.VerifiedLocal,
            ProviderEgressStatus.Local => ModelProviderKind.UserLocal,
            _ => ModelProviderKind.ExternalApi
        };
        var endpoint = disclosure.EgressStatus == ProviderEgressStatus.External ? disclosure.DataPath : string.Empty;
        var modelPath = disclosure.EgressStatus == ProviderEgressStatus.External ? null : disclosure.DataPath;
        var secretRef = disclosure.EgressStatus == ProviderEgressStatus.External ? "cred_" + disclosure.ProviderId : null;

        var selection = new ModelProviderSelection(
            "selection_" + Guid.NewGuid().ToString("N"),
            kind,
            disclosure.ProviderId,
            disclosure.ModelRef,
            endpoint,
            modelPath,
            secretRef,
            disclosure.EgressStatus == ProviderEgressStatus.External,
            true);
        await _store.SaveAsync(selection, cancellationToken);
        return new ProviderSelectionEvent(selection.SelectionId, selection.ProviderRef, selection.ModelRef, selection.Kind, disclosure.EgressStatus, previousSelectionId, AuthoritativeClock.UtcNow);
    }
}