# BT Azure Tools

A collection of CLI tools for common Azure administrative tasks.

## Installation

### macOS (Recommended - via Homebrew)

```bash
# Add the tap (one-time)
brew tap bt-pro-solutions/tap

# Install
brew install bt-azure-tools
```

Or install directly without tapping:
```bash
brew install bt-pro-solutions/tap/bt-azure-tools
```

### Manual Installation (macOS/Linux)

1. Download the latest release for your platform from [GitHub Releases](https://github.com/bt-pro-solutions/bt-azure-tools/releases)
2. Extract the tarball:
   ```bash
   tar -xzf bt-azure-tools-osx-arm64.tar.gz  # or osx-x64 for Intel Macs
   ```
3. Move to a directory in your PATH:
   ```bash
   sudo mv bt-azure-tools /usr/local/bin/
   ```
4. Make executable (if needed):
   ```bash
   chmod +x /usr/local/bin/bt-azure-tools
   ```

### Building from Source

```bash
git clone https://github.com/bt-pro-solutions/bt-azure-tools.git
cd BTAzureTools/BTAzureTools.Console
dotnet build
dotnet run
```

## Prerequisites

- Azure CLI logged in (`az login`)

For building from source:
- .NET 10.0 SDK

## Usage

Simply run:
```bash
bt-azure-tools
```

## Available Tools

### SQL Entra Permissions

Manages Microsoft Entra ID user access to Azure SQL databases. This tool allows you to:

- Add Entra ID users (individual users or managed identities) as database users
- Set permission levels from read-only to full admin
- Modify existing user permissions
- Remove users from databases

#### Permission Levels

| Level | Roles | Description |
|-------|-------|-------------|
| **Full Admin** | `db_owner` | All database permissions including schema changes |
| **Full App-Level** | `db_datareader`, `db_datawriter` + `EXECUTE` | Read, write, and execute stored procedures |
| **Restricted App-Level** | `db_datareader`, `db_datawriter` | Read and write data only |
| **Read-Only** | `db_datareader` | Read data only |
| **None** | (removes user) | Removes the user from the database |

#### How It Works

1. **Select Azure Context**: Choose your tenant and subscription
2. **Select SQL Resources**: Pick the SQL Server and database
3. **Select Principal**: Search for an Entra user by email or a managed identity/app by name
4. **Select Permission Level**: Choose the desired access level
5. **Execute**: The tool temporarily elevates your user to SQL admin to make changes, then restores the original admin

> ⚠️ **Note**: This tool temporarily changes the SQL Server Entra admin to your logged-in user. If someone is actively using the admin account, they may experience brief disruption.

## Architecture

The project follows a clean architecture pattern:

```
BTAzureTools.Console/
├── Core/
│   ├── Abstractions/     # Interfaces for all services
│   └── Domain/           # Domain models (TenantInfo, SqlDatabaseInfo, etc.)
├── Infrastructure/
│   ├── Azure/            # Azure SDK implementations
│   ├── Graph/            # Microsoft Graph implementations
│   └── Sql/              # SQL connection and permission management
├── Cli/                  # Spectre.Console UI components
├── Tools/                # Individual tool implementations
│   └── SqlEntraPermissions/
└── Program.cs            # DI setup and entry point
```

## Adding New Tools

1. Create a new folder under `Tools/`
2. Implement `ITool` interface
3. Register in `Program.cs`:
   ```csharp
   services.AddTransient<YourNewTool>();
   toolRegistry.Register<YourNewTool>();
   ```

## Dependencies

- **Spectre.Console** - Rich CLI interaction
- **Azure.Identity** - Azure authentication
- **Azure.ResourceManager.Sql** - SQL Server management
- **Microsoft.Graph** - Entra ID user/principal lookup
- **Microsoft.Data.SqlClient** - Database operations
