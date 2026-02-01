# Release Process

This document defines the deterministic release procedure for RunForge Desktop.

## Release Gate (must pass before tagging)

### 1. Verify Repository State

```bash
cd "F:\AI\runforge-desktop"

# Confirm correct repo + branch
git remote -v
# Expected: origin https://github.com/mcp-tool-shop-org/runforge-desktop.git

git branch --show-current
# Expected: main
```

### 2. Sync and Clean

```bash
git pull
git status
# Expected: working tree clean (no uncommitted changes)
```

### 3. Build

```bash
dotnet build -c Release
# Expected: 0 errors (warnings acceptable if documented)
```

### 4. Test

```bash
dotnet test -c Release
# Expected: all tests pass
```

## Tagging

Only after the release gate passes:

```bash
git tag -a vX.Y.Z -m "RunForge Desktop vX.Y.Z - <summary>"
git push origin vX.Y.Z
```

Tag naming: `vMAJOR.MINOR.PATCH` (e.g., `v0.1.1`, `v0.2.0`)

## GitHub Release

```bash
gh release create vX.Y.Z --title "vX.Y.Z" --notes-file "docs/RELEASE_NOTES_vX.Y.Z.md"
```

## Artifacts

Attach artifacts **only if** they can be reproduced from documented commands in this repo.

| Artifact | Status | Command |
|----------|--------|---------|
| MSIX Package | Not yet implemented | See [PACKAGING_MSIX.md](./PACKAGING_MSIX.md) |

If an artifact cannot be reliably produced, release without it and document as "planned."

## Release Notes

Create `docs/RELEASE_NOTES_vX.Y.Z.md` before tagging with:

- Overview (1-2 sentences)
- What's New (bullet points)
- Technical Details
- Upgrade Path
- Known Issues
- Download link

## Versioning

- `ApplicationDisplayVersion` in `.csproj`: user-facing semver (e.g., `0.2.0`)
- `ApplicationVersion` in `.csproj`: monotonically increasing integer for Windows updates
- Both must be bumped before release

## Checklist

- [ ] All release gate steps pass
- [ ] Version bumped in `.csproj` / `Package.appxmanifest`
- [ ] Release notes written
- [ ] Tag created and pushed
- [ ] GitHub release created
- [ ] Artifacts attached (if available)
