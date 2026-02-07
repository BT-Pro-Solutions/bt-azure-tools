namespace BTAzureTools.Core.Domain;

/// <summary>
/// An access level option for a specific Azure resource type.
/// </summary>
public sealed record ResourceAccessLevel(
    string Name,
    string Description,
    IReadOnlyList<string> RoleNames);

/// <summary>
/// A supported Azure resource type and its access level options.
/// </summary>
public sealed record SupportedResourceType(
    string Id,
    string DisplayName,
    string AzureResourceType,
    string Description,
    IReadOnlyList<ResourceAccessLevel> AccessLevels,
    string? KindContains = null);

/// <summary>
/// Basic ARM resource details used by IAM assignment flows.
/// </summary>
public sealed record ArmResourceInfo(
    string ResourceId,
    string Name,
    string ResourceGroupName,
    string ResourceType,
    string Location,
    string? Kind);

/// <summary>
/// Catalog of resource types and permission profiles supported by the IAM tool.
/// </summary>
public static class ResourceIamCatalog
{
    public static IReadOnlyList<SupportedResourceType> SupportedTypes { get; } =
    [
        new SupportedResourceType(
            "azure-openai",
            "Azure OpenAI",
            "Microsoft.CognitiveServices/accounts",
            "Assign OpenAI-specific RBAC roles to Cognitive Services OpenAI accounts.",
            [
                new ResourceAccessLevel(
                    "OpenAI User",
                    "Use deployed models and OpenAI endpoints.",
                    ["Cognitive Services OpenAI User"]),
                new ResourceAccessLevel(
                    "OpenAI Contributor",
                    "Manage OpenAI deployments and model operations.",
                    ["Cognitive Services OpenAI Contributor"]),
                new ResourceAccessLevel(
                    "Cognitive Services User",
                    "Use general Cognitive Services data-plane operations.",
                    ["Cognitive Services User"]),
                new ResourceAccessLevel(
                    "Cognitive Services Contributor",
                    "Manage Cognitive Services account settings and deployments.",
                    ["Cognitive Services Contributor"]),
                new ResourceAccessLevel(
                    "Reader",
                    "Read-only access to the resource configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ],
            KindContains: "openai"),
        new SupportedResourceType(
            "storage-account",
            "Azure Storage Account",
            "Microsoft.Storage/storageAccounts",
            "Assign control-plane and data-plane RBAC roles for storage accounts.",
            [
                new ResourceAccessLevel(
                    "Storage Reader",
                    "Read-only access to storage account configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Storage Contributor",
                    "Manage storage account configuration.",
                    ["Storage Account Contributor"]),
                new ResourceAccessLevel(
                    "Storage Owner",
                    "Full control over storage account and access assignments.",
                    ["Owner"]),
                new ResourceAccessLevel(
                    "Blob Reader",
                    "Read blob data only.",
                    ["Storage Blob Data Reader"]),
                new ResourceAccessLevel(
                    "Blob Contributor",
                    "Read and write blob data.",
                    ["Storage Blob Data Contributor"]),
                new ResourceAccessLevel(
                    "Blob Owner",
                    "Full blob data permissions including ACL management.",
                    ["Storage Blob Data Owner"])
            ]),
        new SupportedResourceType(
            "key-vault",
            "Azure Key Vault",
            "Microsoft.KeyVault/vaults",
            "Assign Key Vault RBAC roles for control-plane and data-plane scenarios.",
            [
                new ResourceAccessLevel(
                    "Vault Reader",
                    "Read vault metadata and configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Vault Contributor",
                    "Manage vault configuration (not secret values).",
                    ["Key Vault Contributor"]),
                new ResourceAccessLevel(
                    "Secrets User",
                    "Read secret values.",
                    ["Key Vault Secrets User"]),
                new ResourceAccessLevel(
                    "Secrets Officer",
                    "Manage secret lifecycle and values.",
                    ["Key Vault Secrets Officer"]),
                new ResourceAccessLevel(
                    "Certificates Officer",
                    "Manage certificate lifecycle.",
                    ["Key Vault Certificates Officer"]),
                new ResourceAccessLevel(
                    "Crypto User",
                    "Perform cryptographic operations using vault keys.",
                    ["Key Vault Crypto User"]),
                new ResourceAccessLevel(
                    "Crypto Officer",
                    "Manage key lifecycle and cryptographic policies.",
                    ["Key Vault Crypto Officer"]),
                new ResourceAccessLevel(
                    "Administrator",
                    "Full Key Vault data-plane administration.",
                    ["Key Vault Administrator"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "sql-server",
            "Azure SQL Server",
            "Microsoft.Sql/servers",
            "Assign control-plane RBAC roles for SQL server management.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View SQL server settings and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "SQL Server Contributor",
                    "Manage SQL server configuration and databases.",
                    ["SQL Server Contributor"]),
                new ResourceAccessLevel(
                    "SQL DB Contributor",
                    "Manage SQL databases under the server scope.",
                    ["SQL DB Contributor"]),
                new ResourceAccessLevel(
                    "SQL Security Manager",
                    "Manage SQL security-related settings.",
                    ["SQL Security Manager"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "General management access to this SQL resource scope.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "app-service",
            "Azure App Service",
            "Microsoft.Web/sites",
            "Assign RBAC roles for App Service web apps, APIs, and functions.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View app configuration and metrics.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Website Contributor",
                    "Manage web apps but not access policies.",
                    ["Website Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "function-app",
            "Azure Functions",
            "Microsoft.Web/sites",
            "Assign RBAC roles for Azure Function apps.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View function app configuration and metrics.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Website Contributor",
                    "Manage function apps but not access policies.",
                    ["Website Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ],
            KindContains: "functionapp"),
        new SupportedResourceType(
            "cosmos-db",
            "Azure Cosmos DB",
            "Microsoft.DocumentDB/databaseAccounts",
            "Assign control-plane and data-plane RBAC roles for Cosmos DB accounts.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "Read Cosmos DB account configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Cosmos DB Account Reader",
                    "Read Cosmos DB account and database metadata.",
                    ["Cosmos DB Account Reader Role"]),
                new ResourceAccessLevel(
                    "Cosmos DB Operator",
                    "Manage Cosmos DB accounts but not data access.",
                    ["Cosmos DB Operator"]),
                new ResourceAccessLevel(
                    "Data Reader",
                    "Read data from Cosmos DB containers.",
                    ["Cosmos DB Built-in Data Reader"]),
                new ResourceAccessLevel(
                    "Data Contributor",
                    "Read and write data in Cosmos DB containers.",
                    ["Cosmos DB Built-in Data Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage Cosmos DB account configuration and resources.",
                    ["DocumentDB Account Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "service-bus",
            "Azure Service Bus",
            "Microsoft.ServiceBus/namespaces",
            "Assign RBAC roles for Service Bus messaging namespaces.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Service Bus namespace configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Data Receiver",
                    "Receive messages from Service Bus queues and subscriptions.",
                    ["Azure Service Bus Data Receiver"]),
                new ResourceAccessLevel(
                    "Data Sender",
                    "Send messages to Service Bus queues and topics.",
                    ["Azure Service Bus Data Sender"]),
                new ResourceAccessLevel(
                    "Data Owner",
                    "Full data-plane access to Service Bus resources.",
                    ["Azure Service Bus Data Owner"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage Service Bus namespace configuration.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "event-hub",
            "Azure Event Hubs",
            "Microsoft.EventHub/namespaces",
            "Assign RBAC roles for Event Hub streaming namespaces.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Event Hubs namespace configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Data Receiver",
                    "Receive events from Event Hubs.",
                    ["Azure Event Hubs Data Receiver"]),
                new ResourceAccessLevel(
                    "Data Sender",
                    "Send events to Event Hubs.",
                    ["Azure Event Hubs Data Sender"]),
                new ResourceAccessLevel(
                    "Data Owner",
                    "Full data-plane access to Event Hubs resources.",
                    ["Azure Event Hubs Data Owner"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage Event Hubs namespace configuration.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "container-registry",
            "Azure Container Registry",
            "Microsoft.ContainerRegistry/registries",
            "Assign RBAC roles for container image registries.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View registry configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "AcrPull",
                    "Pull container images from the registry.",
                    ["AcrPull"]),
                new ResourceAccessLevel(
                    "AcrPush",
                    "Push and pull container images.",
                    ["AcrPush"]),
                new ResourceAccessLevel(
                    "AcrDelete",
                    "Delete images and repositories from the registry.",
                    ["AcrDelete"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage registry configuration and resources.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "aks",
            "Azure Kubernetes Service",
            "Microsoft.ContainerService/managedClusters",
            "Assign RBAC roles for AKS managed Kubernetes clusters.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View cluster configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Cluster User",
                    "List cluster user credentials for kubeconfig access.",
                    ["Azure Kubernetes Service Cluster User Role"]),
                new ResourceAccessLevel(
                    "Cluster Admin",
                    "List cluster admin credentials for full kubeconfig access.",
                    ["Azure Kubernetes Service Cluster Admin Role"]),
                new ResourceAccessLevel(
                    "RBAC Reader",
                    "Read-only access to most Kubernetes objects in namespaces.",
                    ["Azure Kubernetes Service RBAC Reader"]),
                new ResourceAccessLevel(
                    "RBAC Writer",
                    "Read/write access to most Kubernetes objects in namespaces.",
                    ["Azure Kubernetes Service RBAC Writer"]),
                new ResourceAccessLevel(
                    "RBAC Admin",
                    "Full access to Kubernetes objects in namespaces and manage roles.",
                    ["Azure Kubernetes Service RBAC Admin"]),
                new ResourceAccessLevel(
                    "RBAC Cluster Admin",
                    "Full cluster-wide access to all Kubernetes objects.",
                    ["Azure Kubernetes Service RBAC Cluster Admin"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage AKS cluster configuration and resources.",
                    ["Azure Kubernetes Service Contributor Role"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "container-app",
            "Azure Container Apps",
            "Microsoft.App/containerApps",
            "Assign RBAC roles for Container Apps serverless containers.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Container App configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "ContainerApp Contributor",
                    "Manage Container Apps and their revisions.",
                    ["Azure ContainerApps Session Executor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "event-grid-topic",
            "Azure Event Grid Topic",
            "Microsoft.EventGrid/topics",
            "Assign RBAC roles for Event Grid custom topics.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Event Grid topic configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Data Sender",
                    "Send events to Event Grid topics.",
                    ["EventGrid Data Sender"]),
                new ResourceAccessLevel(
                    "EventGrid Contributor",
                    "Manage Event Grid topics and subscriptions.",
                    ["EventGrid Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "signalr",
            "Azure SignalR Service",
            "Microsoft.SignalRService/SignalR",
            "Assign RBAC roles for SignalR real-time messaging service.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View SignalR resource configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "SignalR App Server",
                    "Allow app server to access SignalR with AAD auth.",
                    ["SignalR App Server"]),
                new ResourceAccessLevel(
                    "SignalR Service Owner",
                    "Full data-plane access to SignalR Service APIs.",
                    ["SignalR Service Owner"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage SignalR resource configuration.",
                    ["SignalR Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "app-config",
            "Azure App Configuration",
            "Microsoft.AppConfiguration/configurationStores",
            "Assign RBAC roles for App Configuration stores.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View App Configuration store metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Data Reader",
                    "Read configuration key-values and feature flags.",
                    ["App Configuration Data Reader"]),
                new ResourceAccessLevel(
                    "Data Owner",
                    "Read, write, and delete configuration key-values.",
                    ["App Configuration Data Owner"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage App Configuration store settings.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "redis-cache",
            "Azure Cache for Redis",
            "Microsoft.Cache/redis",
            "Assign RBAC roles for Azure Cache for Redis instances.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Redis cache configuration and metrics.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Data Access Reader",
                    "Read data from Redis cache via data-plane.",
                    ["Redis Cache Data Access Reader"]),
                new ResourceAccessLevel(
                    "Data Contributor",
                    "Read and write data in Redis cache via data-plane.",
                    ["Redis Cache Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage Redis cache configuration and resources.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "search-service",
            "Azure AI Search",
            "Microsoft.Search/searchServices",
            "Assign RBAC roles for Azure AI Search (formerly Cognitive Search).",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View search service configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Index Data Reader",
                    "Read data from search indexes.",
                    ["Search Index Data Reader"]),
                new ResourceAccessLevel(
                    "Index Data Contributor",
                    "Read, write, and delete data in search indexes.",
                    ["Search Index Data Contributor"]),
                new ResourceAccessLevel(
                    "Search Service Contributor",
                    "Manage search service configuration and indexes.",
                    ["Search Service Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "postgresql-flexible",
            "Azure Database for PostgreSQL Flexible Server",
            "Microsoft.DBforPostgreSQL/flexibleServers",
            "Assign RBAC roles for PostgreSQL Flexible Server management.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View PostgreSQL server configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Manage PostgreSQL server configuration and databases.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "managed-identity",
            "Managed Identity",
            "Microsoft.ManagedIdentity/userAssignedIdentities",
            "Assign RBAC roles for user-assigned managed identities.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View managed identity properties.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Managed Identity Operator",
                    "Assign and use the managed identity on resources.",
                    ["Managed Identity Operator"]),
                new ResourceAccessLevel(
                    "Managed Identity Contributor",
                    "Create, delete, and manage user-assigned identities.",
                    ["Managed Identity Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "api-management",
            "Azure API Management",
            "Microsoft.ApiManagement/service",
            "Assign RBAC roles for API Management service instances.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "Read-only access to API Management and its APIs.",
                    ["API Management Service Reader Role"]),
                new ResourceAccessLevel(
                    "Service Operator",
                    "Manage the service but not its APIs or policies.",
                    ["API Management Service Operator Role"]),
                new ResourceAccessLevel(
                    "Service Contributor",
                    "Manage the service and its APIs.",
                    ["API Management Service Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "static-web-app",
            "Azure Static Web Apps",
            "Microsoft.Web/staticSites",
            "Assign RBAC roles for Static Web App resources.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Static Web App configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Static Web App Contributor",
                    "Manage Static Web Apps including deployment.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "monitor-workspace",
            "Azure Monitor Workspace",
            "Microsoft.Monitor/accounts",
            "Assign RBAC roles for Azure Monitor (Prometheus) workspaces.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View monitoring data and workspace configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Monitoring Reader",
                    "Read monitoring data from all sources.",
                    ["Monitoring Reader"]),
                new ResourceAccessLevel(
                    "Monitoring Contributor",
                    "Manage monitoring settings and write monitoring data.",
                    ["Monitoring Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "log-analytics",
            "Log Analytics Workspace",
            "Microsoft.OperationalInsights/workspaces",
            "Assign RBAC roles for Log Analytics workspaces.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View workspace configuration and metadata.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Log Analytics Reader",
                    "Read and search all monitoring data.",
                    ["Log Analytics Reader"]),
                new ResourceAccessLevel(
                    "Log Analytics Contributor",
                    "Read monitoring data and manage workspace settings.",
                    ["Log Analytics Contributor"]),
                new ResourceAccessLevel(
                    "Contributor",
                    "Full management access except access control.",
                    ["Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ]),
        new SupportedResourceType(
            "app-insights",
            "Application Insights",
            "Microsoft.Insights/components",
            "Assign RBAC roles for Application Insights resources.",
            [
                new ResourceAccessLevel(
                    "Reader",
                    "View Application Insights configuration.",
                    ["Reader"]),
                new ResourceAccessLevel(
                    "Monitoring Reader",
                    "Read telemetry and monitoring data.",
                    ["Monitoring Reader"]),
                new ResourceAccessLevel(
                    "Monitoring Contributor",
                    "Manage monitoring settings and data.",
                    ["Monitoring Contributor"]),
                new ResourceAccessLevel(
                    "Application Insights Component Contributor",
                    "Manage Application Insights components.",
                    ["Application Insights Component Contributor"]),
                new ResourceAccessLevel(
                    "Owner",
                    "Full control over resource and access assignments.",
                    ["Owner"])
            ])
    ];
}
