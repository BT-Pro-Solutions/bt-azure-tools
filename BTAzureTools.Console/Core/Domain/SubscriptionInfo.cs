namespace BTAzureTools.Core.Domain;

/// <summary>
/// Represents an Azure subscription.
/// </summary>
public sealed record SubscriptionInfo(
    string SubscriptionId,
    string DisplayName,
    Guid TenantId)
{
    public override string ToString() => $"{DisplayName} ({SubscriptionId})";
}
