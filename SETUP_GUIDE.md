# Setup Guide for BTAzureTools Homebrew Distribution

Follow these steps to set up automated releases and Homebrew distribution.

## ✅ Step 1: Push This Repository to GitHub

If you haven't already:

```bash
cd /Users/calebbertsch/BTProjects/BTAzureTools

# Initialize git if needed
git init

# Add all files
git add .
git commit -m "Initial commit with GitHub Actions and Homebrew support"

# Create the repository on GitHub (using gh CLI)
gh repo create bt-pro-solutions/bt-azure-tools --public --source=. --remote=origin

# Or manually create at: https://github.com/new
# Then add remote and push:
git remote add origin https://github.com/bt-pro-solutions/bt-azure-tools.git
git branch -M main
git push -u origin main
```

---

## ✅ Step 2: Create the Homebrew Tap Repository

```bash
# Create the homebrew-tap repository
gh repo create bt-pro-solutions/homebrew-tap --public

# Clone it locally
cd ~/Downloads  # or wherever you want to work
git clone https://github.com/bt-pro-solutions/homebrew-tap.git
cd homebrew-tap

# Create the Formula directory
mkdir Formula
mkdir -p .github/workflows

# Copy the formula and workflow from BTAzureTools
cp /Users/calebbertsch/BTProjects/BTAzureTools/docs/homebrew/Formula/bt-azure-tools.rb Formula/
cp /Users/calebbertsch/BTProjects/BTAzureTools/docs/homebrew/.github-workflows/update-formula.yml .github/workflows/

# Commit and push
git add .
git commit -m "Add bt-azure-tools formula"
git push origin main
```

---

## ✅ Step 3: Create GitHub Personal Access Token

1. Go to https://github.com/settings/tokens
2. Click "Generate new token" → "Generate new token (classic)"
3. Name it: `BTAzureTools Homebrew Tap`
4. Select scopes:
   - ✅ `repo` (all repo permissions)
5. Click "Generate token"
6. **Copy the token** (you won't see it again!)

---

## ✅ Step 4: Add Secrets & Variables to bt-azure-tools Repository

In the **bt-azure-tools** repository (NOT the homebrew-tap):

### Add Secret:
1. Go to: https://github.com/bt-pro-solutions/bt-azure-tools/settings/secrets/actions
2. Click "New repository secret"
3. Name: `HOMEBREW_TAP_TOKEN`
4. Value: [Paste the token from Step 3]
5. Click "Add secret"

### Add Variable:
1. Go to: https://github.com/bt-pro-solutions/bt-azure-tools/settings/variables/actions
2. Click "New repository variable"
3. Name: `HOMEBREW_TAP_REPO`
4. Value: `bt-pro-solutions/homebrew-tap`
5. Click "Add variable"

---

## ✅ Step 5: Create Your First Release

```bash
cd /Users/calebbertsch/BTProjects/BTAzureTools

# Make sure everything is committed
git add .
git commit -m "Ready for v1.0.0 release"
git push

# Create and push the tag
git tag v1.0.0
git push origin v1.0.0
```

This will automatically:
1. Build binaries for macOS (ARM & Intel), Linux, and Windows
2. Create a GitHub Release with the binaries
3. Update your Homebrew tap formula

---

## ✅ Step 6: Verify the Release

1. Check the Actions tab: https://github.com/bt-pro-solutions/bt-azure-tools/actions
2. Wait for the "Release" workflow to complete (~5-10 minutes)
3. Check the release: https://github.com/bt-pro-solutions/bt-azure-tools/releases
4. Verify the homebrew-tap was updated: https://github.com/bt-pro-solutions/homebrew-tap/commits/main

---

## ✅ Step 7: Test Installation

```bash
# Add the tap
brew tap bt-pro-solutions/tap

# Install
brew install bt-azure-tools

# Test
bt-azure-tools --version
```

---

## Troubleshooting

### GitHub Actions failing?
- Check you pushed all the workflow files to `.github/workflows/`
- Verify the secrets and variables are set correctly

### Homebrew formula not updating?
- Check the `HOMEBREW_TAP_TOKEN` has the right permissions
- Check the `HOMEBREW_TAP_REPO` variable is exactly: `bt-pro-solutions/homebrew-tap`

### Can't push tags?
```bash
git tag -d v1.0.0  # Delete local tag
git push origin :refs/tags/v1.0.0  # Delete remote tag
git tag v1.0.0  # Recreate
git push origin v1.0.0  # Push again
```

---

## Future Releases

For version 1.0.1, 1.1.0, etc.:

```bash
# Update version in BTAzureTools.Console/BTAzureTools.Console.csproj if desired
# Commit changes
git add .
git commit -m "Version 1.0.1"
git push

# Tag and push
git tag v1.0.1
git push origin v1.0.1
```

That's it! The GitHub Action handles everything else.
