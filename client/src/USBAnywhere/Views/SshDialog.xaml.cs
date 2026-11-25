using System.Diagnostics;
using System.IO;
using System.Windows;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Views;

public partial class SshDialog : Window
{
    private readonly ILocalizationService _localization;
    private readonly string _language;

    public SshDialog(ILocalizationService localization, string language, string serverIP)
    {
        InitializeComponent();

        _localization = localization;
        _language = language;

        txtIP.Text = serverIP;

        UpdateTexts();
    }

    private void UpdateTexts()
    {
        Title = _localization.GetText("ssh_title", _language);
        lblUser.Text = _localization.GetText("ssh_user", _language);
        btnConnect.Content = _localization.GetText("ssh_connect", _language);
        btnCopy.Content = _localization.GetText("ssh_copy", _language);
        btnAutoSetup.Content = _localization.GetText("ssh_auto_setup", _language);
        btnClose.Content = _localization.GetText("ssh_close", _language);

        // Build instructions
        txtInstructions.Text = $"{_localization.GetText("ssh_instructions", _language)}\n\n" +
            $"{_localization.GetText("ssh_step1", _language)}\n" +
            "   sudo apt update\n" +
            "   sudo apt install usbip hwdata usbutils\n\n" +
            $"{_localization.GetText("ssh_step2", _language)}\n" +
            "   sudo modprobe usbip_host\n\n" +
            $"{_localization.GetText("ssh_step3", _language)}\n" +
            "   sudo usbipd\n\n" +
            $"{_localization.GetText("ssh_step4", _language)}\n" +
            "   usbip list -l\n\n" +
            $"{_localization.GetText("ssh_step5", _language)}\n" +
            "   sudo usbip bind -b 1-1.4\n\n" +
            $"{_localization.GetText("ssh_step6", _language)}\n" +
            "   modprobe usbip_host && usbipd -D";
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var user = txtUser.Text.Trim();
        var ip = txtIP.Text.Trim();

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(ip))
            return;

        // Input validation: Prevent Command/Option Injection (no leading '-', no shell metacharacters)
        if (!USBAnywhere.Utils.InputValidator.IsValidSshUser(user) ||
            !USBAnywhere.Utils.InputValidator.IsValidHost(ip))
        {
            System.Windows.MessageBox.Show(
                _localization.GetText("ssh_invalid_chars", _language),
                _localization.GetText("ssh_security_title", _language),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Close();
        LaunchTerminalSsh(user, ip);
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var commands = "TMPFILE=$(mktemp /tmp/usbanywhere-install.XXXXXX.sh) && trap 'rm -f \"$TMPFILE\"' EXIT && (curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh -o \"$TMPFILE\" || wget -qO \"$TMPFILE\" https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh) && chmod 700 \"$TMPFILE\" && sudo bash \"$TMPFILE\"";
        System.Windows.Clipboard.SetText(commands);
        btnCopy.Content = _localization.GetText("ssh_copied", _language);
    }

    private void BtnAutoSetup_Click(object sender, RoutedEventArgs e)
    {
        var user = txtUser.Text.Trim();
        var ip = txtIP.Text.Trim();

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(ip))
            return;

        if (!USBAnywhere.Utils.InputValidator.IsValidSshUser(user) ||
            !USBAnywhere.Utils.InputValidator.IsValidHost(ip))
        {
            System.Windows.MessageBox.Show(
                _localization.GetText("ssh_invalid_chars", _language),
                _localization.GetText("ssh_security_title", _language),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Close();

        // Avoid ';' because Windows Terminal (wt.exe) parses ';' as its own multi-tab command delimiter.
        const string remoteScript = "TMPFILE=$(mktemp /tmp/usbanywhere-install.XXXXXX.sh) && trap 'rm -f \"$TMPFILE\"' EXIT && (curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh -o \"$TMPFILE\" || wget -qO \"$TMPFILE\" https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh) && chmod 700 \"$TMPFILE\" && sudo bash \"$TMPFILE\"";
        LaunchTerminalSsh(user, ip, remoteScript);
    }

    private static void LaunchTerminalSsh(string user, string ip, string? remoteCommand = null)
    {
        try
        {
            var wtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
            var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var psi = new ProcessStartInfo { UseShellExecute = false };

            if (File.Exists(wtPath))
            {
                psi.FileName = wtPath;
                psi.ArgumentList.Add("new-tab");
                psi.ArgumentList.Add("ssh");
            }
            else
            {
                psi.FileName = File.Exists(cmdPath) ? cmdPath : "cmd.exe";
                psi.ArgumentList.Add("/k");
                psi.ArgumentList.Add("ssh");
            }

            if (!string.IsNullOrEmpty(remoteCommand))
                psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add($"{user}@{ip}");
            if (!string.IsNullOrEmpty(remoteCommand))
                psi.ArgumentList.Add(remoteCommand);

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SshDialog] Failed to launch ssh: {ex}");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
