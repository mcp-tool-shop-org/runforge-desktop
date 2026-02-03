# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.9.x   | :white_check_mark: |
| < 0.9   | :x:                |

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

### How to Report

1. **Do NOT** open a public GitHub issue for security vulnerabilities
2. Email security concerns to: **64996768+mcp-tool-shop@users.noreply.github.com**
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes (optional)

### What to Expect

- **Acknowledgment**: Within 48 hours of your report
- **Initial Assessment**: Within 7 days
- **Resolution Timeline**: Depends on severity
  - Critical: 24-72 hours
  - High: 1-2 weeks
  - Medium: 2-4 weeks
  - Low: Next scheduled release

### Scope

This security policy covers:
- RunForge Desktop application (this repository)
- MSIX installer packages
- Configuration file handling
- Local data storage

### Out of Scope

- Third-party dependencies (report to respective maintainers)
- Python CLI training scripts (separate repository)
- Issues requiring physical access to the machine

## Security Best Practices for Users

1. **Download from official sources only**
   - GitHub Releases: https://github.com/mcp-tool-shop-org/runforge-desktop/releases
   - Microsoft Store (when available)

2. **Verify installer integrity**
   - Check SHA256 checksums provided in release notes
   - Verify MSIX signature before installation

3. **Keep software updated**
   - Enable automatic updates when available
   - Subscribe to release notifications

## Security Features

- **No network telemetry**: App operates fully offline
- **No account required**: No credentials stored
- **Local-only data**: All data stays on your machine
- **Signed packages**: MSIX installers are code-signed
- **No elevated privileges**: Runs without admin rights

## Acknowledgments

We appreciate responsible disclosure and will acknowledge security researchers who report valid vulnerabilities (with permission).
