# 🛡️ Security Policy — USBAnywhere

🌐 **Language / Idioma:** **English** | [Español](SECURITY_ES.md)

## Supported Versions

| Version | Supported          | Status |
| ------- | ------------------ | ------ |
| 3.0.x   | :white_check_mark: | Active (Current: v3.0.1) |
| < 3.0   | :x:                | Unsupported (Upgrade to USBAnywhere v3.0.1) |

---

## Security Architecture & Zero-Trust Hardening

USBAnywhere is designed with a defense-in-depth, Zero-Trust security model across the ecosystem.

The product uses a combination of input validation, restricted process invocation, trusted host allowlists, and signed driver expectations as part of its overall security posture. These controls are intended to reduce the risk of unsafe execution paths and to improve operational trust.

---

## Security Architecture & Zero-Trust Hardening

**USBAnywhere** is designed with a defense-in-depth, Zero-Trust security model across the entire ecosystem:

### 1. Whitelist Command & Argument Sanitization (`InputValidator`)
- All IPv4 addresses, domain names, USB `bus-id` strings, and TCP ports are strictly validated against restrictive regular expression whitelists before any shell or system process invocation.
- External processes (`usbipw.exe`, `devnode.exe`, `pnputil.exe`) are invoked with `UseShellExecute = false`, preventing arbitrary argument injection and shell command chaining.

### 2. Digital Signatures & Authenticode Verification (`SignatureVerifier`)
- All distributed native executables (`usbipw.exe`, `devnode.exe`, `USBAnywhere.exe`) are validated for valid Microsoft Authenticode digital signatures.
- Binary digests are compared against verified cryptographic manifests before invoking device attachment or detachment.

### 3. Fail-Closed Cryptographic Auto-Updater (`UpdateService`)
- The integrated update engine strictly communicates over HTTPS and whitelists official GitHub release endpoints (`github.com`, `objects.githubusercontent.com`).
- Update installers are verified against an official `SHA256SUMS.txt` cryptographic manifest before execution. If the SHA-256 hash fails or does not match, the file is deleted immediately and execution is aborted.

### 4. Microsoft WHQL-Certified Drivers
- Uses Microsoft WHQL-signed `usbip2_ude.sys` and `usbip2_filter.sys` drivers, fully compatible with Windows 10 & 11 **Core Isolation / Memory Integrity (HVCI)**.
- Does not require Windows Test Signing Mode on standard x64 systems.

### 5. Multiplatform Server Security Models
- **Windows Server**: The Windows Firewall rule for port 3240 is restricted exclusively to **Private** and **Domain** network profiles, preventing exposure on untrusted Public Wi-Fi networks.
- **macOS Server**: Utilizes native Apple **Userspace IOKit** (`IOKit.framework`). Operates without third-party Kernel Extensions (`.kext`) and retains complete compatibility with Apple Silicon **System Integrity Protection (SIP)**. Privileges are managed securely via system `launchd` LaunchDaemons.
- **Linux Server**: Runs via a dedicated systemd service and confines USB export operations via targeted udev rules.

---

## Reporting a Vulnerability

If you discover a security issue or vulnerability, please responsibly report it:
- **GitHub Security Advisory**: Open a private advisory at [USBAnywhere Security Advisories](https://github.com/oscarlosu-lab/USBAnywhere/security/advisories/new)
- **Email**: Reach out to the maintainers at [oscarlosu-lab](https://github.com/oscarlosu-lab).

We appreciate responsible disclosure and will investigate and address reported vulnerabilities promptly.
