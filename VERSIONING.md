# Versioning Policy

## Semantic Versioning
This project follows [Semantic Versioning 2.0.0](https://semver.org/).

### Format: `MAJOR.MINOR.PATCH`

- **MAJOR**: Incompatible API changes or fundamental architecture rewrites (e.g., Electron -> .NET).
- **MINOR**: Backward-compatible functionality additions.
- **PATCH**: Backward-compatible bug fixes.

## Current Version: 2.0.0
- **Transition:** Rewrote application from Electron (v1.x) to .NET 8 WPF (v2.0).
- **Breaking Change:** Complete change of underlying runtime requirements (.NET Runtime vs Node/Electron).

## Build Versioning
Builds may be tagged with `+build.<number>` or `-alpha/beta` pre-release identifiers.
Example: `2.0.0-beta.1`
