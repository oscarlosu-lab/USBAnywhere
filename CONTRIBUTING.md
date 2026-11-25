# Contributing to USBAnywhere

Thank you for helping improve **USBAnywhere**!

## Before You Start
- Check existing issues and pull requests to avoid duplicate effort.
- Security vulnerabilities must **not** be opened as public issues — please use **GitHub Security Advisories** (see `SECURITY.md`).

## Development & Verification
1. Fork the repository and create a focused branch from `main`.
2. Keep changes minimal, clean, and well-tested.
3. Verify the full test suite passes locally before opening a Pull Request:
   ```powershell
   dotnet test USBAnywhere.sln
   ```
4. Never commit local machine paths, credentials, tokens, or personal data.

## Pull Request Checklist
- Clear description of the problem and solution.
- Unit tests added or updated for new behavior or bug fixes.
- Zero regressions on existing tests.
