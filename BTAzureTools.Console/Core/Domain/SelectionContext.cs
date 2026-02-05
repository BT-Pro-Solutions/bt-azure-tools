namespace BTAzureTools.Core.Domain;

/// <summary>
/// Contains the selected Azure context (tenant, subscription).
/// </summary>
public sealed record SelectionContext(
    TenantInfo Tenant,
    SubscriptionInfo Subscription);
