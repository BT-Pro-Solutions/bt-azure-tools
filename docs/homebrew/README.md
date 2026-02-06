# Homebrew Tap Setup for BTAzureTools

This directory contains templates for setting up a Homebrew tap to distribute BTAzureTools.

## Quick Setup

1. **Create a new GitHub repository** named `homebrew-tap` in your organization

2. **Copy the formula** from `Formula/bt-azure-tools.rb` to your new repo under `Formula/`

3. **Add repository secrets** to your BTAzureTools repo:
   - `HOMEBREW_TAP_TOKEN`: A GitHub Personal Access Token with `repo` scope for your homebrew-tap repo

4. **Add repository variables** to your BTAzureTools repo:
   - `HOMEBREW_TAP_REPO`: The full repo name (e.g., `bt-pro-solutions/homebrew-tap`)

5. **Copy the workflow** from `update-formula.yml` to your homebrew-tap repo under `.github/workflows/`

## For Team Members

Once set up, team members install with:

```bash
# Add the tap (one-time)
brew tap your-org/tap

# Install
brew install bt-azure-tools

# Or install directly (no need to tap first)
brew install your-org/tap/bt-azure-tools
```

Then just run `bt-azure-tools` from anywhere!

## Manual Release Process

If you prefer manual releases:

1. Build the release locally:
   ```bash
   dotnet publish BTAzureTools.Console/BTAzureTools.Console.csproj \
     --configuration Release \
     --runtime osx-arm64 \
     --self-contained true \
     -p:PublishSingleFile=true \
     -p:IncludeNativeLibrariesForSelfExtract=true \
     -p:IncludeAllContentForSelfExtract=true \
     -p:EnableCompressionInSingleFile=false \
     --output ./publish
   ```

2. Create a tarball:
   ```bash
   cd publish
   tar -czvf bt-azure-tools-osx-arm64.tar.gz BTAzureTools.Console
   ```

3. Upload to GitHub Releases

4. Update the formula with new version and SHA256
