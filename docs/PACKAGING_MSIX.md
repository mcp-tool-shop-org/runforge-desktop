# MSIX Packaging

## Status: Not Yet Implemented

MSIX packaging for RunForge Desktop is planned for **Phase 5** (v0.7–0.9) per the [v1 Roadmap](./RunForge%20v1%20Roadmap.txt).

## Target

- Self-contained MSIX package
- Signed with code-signing certificate
- Automatic updates via `.appinstaller`
- No runtime dependencies for end users

## Acceptance Criteria

When implemented, this feature must:

1. **Build reproducibly** from a single documented command
2. **Sign correctly** with timestamping for long-lived validity
3. **Install cleanly** on Windows 10 1809+ and Windows 11
4. **Update smoothly** without data loss or permission issues
5. **Uninstall completely** with no orphaned files

## Planned Implementation

```bash
# Target command (not yet working)
dotnet publish -c Release -f net10.0-windows10.0.19041.0 \
  -p:WindowsPackageType=MSIX \
  -p:GenerateAppxPackageOnBuild=true
```

## Dependencies

- Code-signing certificate (self-signed for dev, real cert for distribution)
- Certificate installed in build machine's cert store
- Publisher identity matching cert subject

## References

- [Windows App SDK MSIX docs](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/)
- [.NET MAUI packaging](https://learn.microsoft.com/en-us/dotnet/maui/windows/deployment/overview)

---

*This document will be updated when MSIX packaging is implemented.*
