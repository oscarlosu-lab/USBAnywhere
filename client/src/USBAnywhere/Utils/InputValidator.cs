// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System.Net;
using System.Text.RegularExpressions;

namespace USBAnywhere.Utils;

/// <summary>
/// Input validation. Every value that ends up in a process command line
/// MUST pass through here first (the app runs elevated).
/// </summary>
public static class InputValidator
{
    // Hostname labels: alnum, inner hyphens. No leading '-' (prevents option injection).
    private static readonly Regex HostRegex = new(
        @"^(?=.{1,253}$)[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // USB bus id, e.g. "3-1", "1-2.4"
    private static readonly Regex BusIdRegex = new(
        @"^[0-9]{1,4}(-[0-9]{1,4})*(\.[0-9]{1,4})*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PortRegex = new(
        @"^[0-9]{1,3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>IPv4/IPv6 literal or RFC-1123 hostname (never starting with '-').</summary>
    public static bool IsValidHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Contains('%')) return false;
        if (IPAddress.TryParse(value, out _)) return !value.StartsWith('-');
        return HostRegex.IsMatch(value);
    }

    public static bool IsValidBusId(string? value) =>
        !string.IsNullOrEmpty(value) && BusIdRegex.IsMatch(value);

    public static bool IsValidPort(string? value) =>
        !string.IsNullOrEmpty(value) && PortRegex.IsMatch(value);

    /// <summary>SSH user name (POSIX-ish): must not start with '-'.</summary>
    public static bool IsValidSshUser(string? value) =>
        !string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^[A-Za-z0-9_][A-Za-z0-9_\-\.]{0,31}$");
}
