# Code Signing Policy

This document outlines the code signing policy for **USBAnywhere**, specifying how official release binaries are built, signed, and cryptographically verified.

## SignPath Foundation Acknowledgment
* **Free code signing provided by [SignPath.io](https://signpath.io/)**
* **Certificate by [SignPath Foundation](https://signpath.org/)**

Official release binaries for Windows are signed using the SignPath Foundation open-source code-signing service. Private keys are securely managed within SignPath's cloud-based Hardware Security Module (HSM), ensuring keys are never exposed and signatures are strictly governed by automated CI/CD security policies.

---

## Scope & Signed Artifacts
This policy applies to official production release artifacts distributed via GitHub Releases and official download channels:

| Artifact Pattern | Format | Target Platform | Description |
|---|---|---|---|
| `USBAnywhere_Setup_v*.exe` | Inno Setup Installer | Windows x64 | USBAnywhere Client desktop installer |
| `USBAnywhere.exe` | PE32+ Executable | Windows x64 / ARM64 | Self-contained client application binary |
| `USBAnywhere-Server_Setup_v*.exe` | Inno Setup Installer | Windows x64 | USBAnywhere Server desktop installer |
| `USBAnywhere-Server.exe` | PE32+ Executable | Windows x64 | Self-contained server application binary |

---

## Team Roles & Security Governance
In compliance with SignPath Foundation security requirements:

* **Project Owner & Release Manager**:
  * Oscar Losu ([@oscarlosu-lab](https://github.com/oscarlosu-lab))
* **Account Security & Multi-Factor Authentication (MFA)**:
  * All maintainers with commit, release, or repository administrative rights have Multi-Factor Authentication (MFA/2FA) strictly enabled on GitHub.
  * Access to the SignPath organization and API tokens is restricted strictly to authenticated maintainers with MFA.

---

## Release & Build Provenance
1. **Source Transparency**: All source code is developed openly in public repositories under the OSI-approved **GNU General Public License v3.0 (GPL-3.0)**.
2. **Automated Trusted Builds**: Release binaries are compiled exclusively via version-controlled GitHub Actions workflows (`.github/workflows/sign-release.yml` and `Publish-Release.ps1`). Local, ad-hoc, or untrusted binary builds are never submitted for official signing.
3. **Reproducibility & Traceability**: Every signed binary is directly traceable to a signed Git commit and release tag (`v*`) in the official repository.
4. **Binary Integrity Check**: Companion DLLs and native helper tools are cryptographically hashed with SHA-256 and fail-closed integrity gates.

---

## Privacy Statement
*This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.*

---

## Verification
End users and system administrators can verify the authenticity of any official USBAnywhere binary on Windows:
1. Right-click the `.exe` file and select **Properties**.
2. Navigate to the **Digital Signatures** tab.
3. Select the signature and click **Details**.
4. Confirm that the signature is valid, accompanied by an official RFC 3161 timestamp, and chains up to the **SignPath Foundation** trusted root certificate authority.
