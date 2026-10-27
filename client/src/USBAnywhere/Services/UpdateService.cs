using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace USBAnywhere.Services;

/// <summary>
/// Update service - Check for updates against GitHub releases
/// Port from PowerShell Check-ForUpdates / Start-Update
/// </summary>
public class UpdateService : IUpdateService
{
    private const string GITHUB_REPO = "oscarlosu-lab/USBAnywhere";
    private readonly string _currentVersion;
    private readonly ILogger<UpdateService> _logger;
    private readonly IEnvironmentService _env;
    private readonly HttpClient _httpClient;

    public UpdateService(ILogger<UpdateService> logger, IEnvironmentService env)
    {
        _logger = logger;
        _env = env;
        _currentVersion = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "2.0.0";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "USBAnywhere-WPF/3.0.0");
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates from GitHub...");
            var apiUrl = $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest";
            
            var response = await _httpClient.GetStringAsync(apiUrl, ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            if (release == null)
            {
                _logger.LogWarning("Failed to deserialize GitHub release data.");
                return new UpdateInfo { UpdateAvailable = false };
            }
            
            var latestVersionStr = release.TagName?.TrimStart('v') ?? "0.0.0";
            
            if (Version.TryParse(latestVersionStr, out var latest) && 
                Version.TryParse(_currentVersion, out var current) && 
                latest > current)
            {
                _logger.LogInformation("New version available: {Version}", latestVersionStr);
                
                // Find appropriate asset based on portability
                var assets = release.Assets ?? new List<GitHubAsset>();
                GitHubAsset? selectedAsset = null;

                if (_env.IsPortable)
                {
                    var archTag = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
                    selectedAsset = assets.FirstOrDefault(a =>
                        a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true &&
                        (a.Name.Contains(archTag, StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Portable", StringComparison.OrdinalIgnoreCase)));
                }
                if (_env.IsPortable && selectedAsset == null)
                {
                    selectedAsset = assets.FirstOrDefault(a => 
                        a.Name?.Contains("Portable", StringComparison.OrdinalIgnoreCase) == true &&
                        (a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true || 
                         a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true));
                }
                
                // Fallback or Setup
                if (selectedAsset == null)
                {
                    selectedAsset = assets.FirstOrDefault(a => 
                        a.Name?.Contains("Setup", StringComparison.OrdinalIgnoreCase) == true &&
                        a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
                }

                // If still null, just take the first exe
                selectedAsset ??= assets.FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
                
                var hashAsset = assets.FirstOrDefault(a => 
                    a.Name?.Contains("sha256", StringComparison.OrdinalIgnoreCase) == true ||
                    a.Name?.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) == true);
                
                return new UpdateInfo
                {
                    UpdateAvailable = true,
                    LatestVersion = latestVersionStr,
                    CurrentVersion = _currentVersion,
                    DownloadUrl = selectedAsset?.BrowserDownloadUrl,
                    InstallerName = selectedAsset?.Name,
                    HashUrl = hashAsset?.BrowserDownloadUrl,
                    ReleaseNotes = release.Body
                };
            }
            
            _logger.LogInformation("Application is up to date (Current: {Version})", _currentVersion);
            return new UpdateInfo
            {
                UpdateAvailable = false,
                LatestVersion = latestVersionStr,
                CurrentVersion = _currentVersion
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update check cancelled.");
            return new UpdateInfo { UpdateAvailable = false, Error = "Operation cancelled" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates.");
            return new UpdateInfo
            {
                UpdateAvailable = false,
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> StartUpdateAsync(string downloadUrl, string installerName, string? hashUrl = null, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting update process for {Installer}...", installerName);

            // === SECURITY VALIDATION ===
            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(installerName))
                return OperationResult.Fail("Invalid download parameters");
            
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
                return OperationResult.Fail("Invalid download URL format");
            
            var allowedHosts = new[] { "github.com", "objects.githubusercontent.com", "github-releases.githubusercontent.com" };
            bool IsAllowedHost(Uri u) => u.Scheme == Uri.UriSchemeHttps &&
                allowedHosts.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                                      u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
            if (!IsAllowedHost(uri))
            {
                _logger.LogCritical("Blocked untrusted download host: {Host}", uri.Host);
                return OperationResult.Fail($"Untrusted download host: {uri.Host}");
            }

            // Hash is MANDATORY (fail-closed): never run an unverified installer.
            if (string.IsNullOrEmpty(hashUrl) || !Uri.TryCreate(hashUrl, UriKind.Absolute, out var hashUri) || !IsAllowedHost(hashUri))
            {
                _logger.LogCritical("Update aborted: missing or untrusted SHA256 source ({Url})", hashUrl);
                return OperationResult.Fail("Security Error: release has no verifiable SHA256 checksum.");
            }
            
            if (installerName.Contains("..") || Path.GetFileName(installerName) != installerName)
            {
                _logger.LogCritical("Blocked potentially malicious installer name: {Name}", installerName);
                return OperationResult.Fail("Invalid installer name");
            }
            
            if (!installerName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && 
                !installerName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return OperationResult.Fail("Update package must be .exe or .zip");
            // === END SECURITY VALIDATION ===
            
            var tempDir = _env.TempPath;
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            
            var installerPath = Path.Combine(tempDir, installerName);
            _logger.LogInformation("Downloading installer to: {Path}", installerPath);
            
            // Download with progress
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct);
                        totalRead += read;

                        if (totalBytes.HasValue)
                        {
                            progress?.Report((double)totalRead / totalBytes.Value * 100);
                        }
                    }
                }
            }
            
            // === SHA256 VERIFICATION (mandatory) ===
            _logger.LogInformation("Downloading hash for verification: {Url}", hashUrl);
            var hashResponse = await _httpClient.GetStringAsync(hashUrl, ct);

            // Supports a bare hash file (single hash) or a SHA256SUMS list ("<hash>  <file>"): pick the line for our installer.
            string? expectedHash = null;
            var allHashes = System.Text.RegularExpressions.Regex.Matches(hashResponse, @"\b([a-fA-F0-9]{64})\b");
            foreach (var line in hashResponse.Split('\n'))
            {
                if (line.Contains(installerName, StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"\b([a-fA-F0-9]{64})\b");
                    if (m.Success) { expectedHash = m.Value; break; }
                }
            }
            if (expectedHash == null && allHashes.Count == 1)
                expectedHash = allHashes[0].Value;

            if (expectedHash == null)
            {
                _logger.LogCritical("Could not find SHA256 for {Installer} in {Url}", installerName, hashUrl);
                if (File.Exists(installerPath)) File.Delete(installerPath);
                return OperationResult.Fail("Security Error: no matching SHA256 entry for the installer.");
            }

            _logger.LogInformation("Verifying SHA256: {Expected}", expectedHash);
            using (var sha256 = SHA256.Create())
            using (var fs = File.OpenRead(installerPath))
            {
                var hashBytes = await sha256.ComputeHashAsync(fs, ct);
                var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                if (actualHash != expectedHash.ToLowerInvariant())
                {
                    _logger.LogCritical("SHA256 MISMATCH! Expected: {Expected}, Actual: {Actual}", expectedHash, actualHash);
                    fs.Close();
                    if (File.Exists(installerPath)) File.Delete(installerPath);
                    return OperationResult.Fail("Security Error: Hash mismatch. The file may be corrupted or tampered.");
                }
                _logger.LogInformation("SHA256 Verified successfully.");
            }
            // === END SHA256 VERIFICATION ===
            
            if (File.Exists(installerPath))
            {
                _logger.LogInformation("Installer verified and ready. Launching...");
                
                if (installerPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Update downloaded as ZIP. Directing user to manual update for safety.");
                    // Open the folder
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{installerPath}\"");
                    return OperationResult.Fail("Update downloaded as ZIP. Please extract it manually to update the portable version.");
                }

                // Run installer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                
                // Request graceful shutdown via main window if running in WPF context
                if (System.Windows.Application.Current != null)
                {
                    if (System.Windows.Application.Current.MainWindow != null)
                    {
                        System.Windows.Application.Current.MainWindow.Close();
                    }
                    else
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
                
                return OperationResult.Ok();
            }
            
            return OperationResult.Fail("Could not verify downloaded installer");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update process cancelled.");
            return OperationResult.Fail("Update cancelled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during update.");
            return OperationResult.Fail(ex.Message);
        }
    }

    private class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string? Body { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}


