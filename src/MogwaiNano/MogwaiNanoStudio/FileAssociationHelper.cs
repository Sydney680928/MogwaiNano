// Copyright 2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MogwaiNanoStudio;

/// <summary>
/// Registers the .mog file extension with MOGWAI_CLI at startup.
/// Called once per run; exits immediately if the association is already current.
///
/// Platform support:
///   Windows  — HKCU\Software\Classes (no admin rights required) + SHChangeNotify
///   Linux    — XDG MIME + .desktop file (GNOME, KDE and compatible DE)
///   macOS    — Launch Services via lsregister (best-effort, no .app bundle)
/// </summary>
internal static class FileAssociationHelper
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const string Extension     = ".mog";
    private const string ProgId        = "MOGWAI.Script";        // Windows ProgId
    private const string FileTypeLabel = "MOGWAI Script";
    private const string MimeType      = "application/x-mogwai"; // Linux / macOS
    private const string DesktopId     = "mogwai-cli.desktop";   // Linux

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Ensures .mog files are associated with MOGWAI_CLI.
    /// Displays a non-blocking warning on failure and never throws.
    /// </summary>
    public static void EnsureFileAssociation()
    {
        try
        {
            if      (OperatingSystem.IsWindows()) EnsureWindowsAssociation();
            else if (OperatingSystem.IsLinux())   EnsureLinuxAssociation();
            else if (OperatingSystem.IsMacOS())   EnsureMacOSAssociation();
        }
        catch (Exception ex)
        {
            Warn($"Could not register {Extension} file association: {ex.Message}");
        }
    }

    // ── Windows ───────────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsAssociation()
    {
        var exePath = GetExePath();
        if (exePath is null)
        {
            Warn("Cannot determine executable path; file association skipped.");
            return;
        }

        // HKCU\Software\Classes — writable without elevation
        using var classes = Microsoft.Win32.Registry.CurrentUser
                                    .OpenSubKey(@"Software\Classes", writable: true);
        if (classes is null)
        {
            Warn(@"Cannot open HKCU\Software\Classes; file association skipped.");
            return;
        }

        // Nothing to do if the association already points to us
        if (IsWindowsAssociationCurrent(classes, exePath))
            return;

        // .mog → ProgId
        using (var extKey = classes.CreateSubKey(Extension))
        {
            extKey.SetValue(null, ProgId);
            extKey.SetValue("Content Type", MimeType);
            // "Perceived Type" helps Windows choose the right preview handler
            extKey.SetValue("PerceivedType", "text");
        }

        // ProgId subtree
        using (var progIdKey = classes.CreateSubKey(ProgId))
        {
            progIdKey.SetValue(null, FileTypeLabel);

            // Icon — index 0 = first icon resource inside the exe
            using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                iconKey.SetValue(null, $"{exePath},0");

            // Double-click / Enter handler
            using (var cmdKey = progIdKey.CreateSubKey(@"shell\open\command"))
                cmdKey.SetValue(null, $"\"{exePath}\" \"%1\"");
        }

        // Notify Explorer of the change (refreshes icons and context menus)
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

        Console.WriteLine($"[MOGWAI] {Extension} files are now associated with MOGWAI_CLI.");
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsAssociationCurrent(
        Microsoft.Win32.RegistryKey classes, string exePath)
    {
        using var extKey = classes.OpenSubKey(Extension);
        if (extKey?.GetValue(null) as string != ProgId)
            return false;

        using var cmdKey = classes.OpenSubKey($@"{ProgId}\shell\open\command");
        var cmd = cmdKey?.GetValue(null) as string ?? string.Empty;

        // Compare normalized paths to handle casing and trailing slashes
        return cmd.Contains(
            Path.GetFullPath(exePath),
            StringComparison.OrdinalIgnoreCase);
    }

    // Shell notification constants
    private const uint SHCNE_ASSOCCHANGED = 0x0800_0000;
    private const uint SHCNF_IDLIST       = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern void SHChangeNotify(
        uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    // ── Linux ─────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    private static void EnsureLinuxAssociation()
    {
        var exePath = GetExePath();
        if (exePath is null)
        {
            Warn("Cannot determine executable path; file association skipped.");
            return;
        }

        var home    = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var mimeDir = Path.Combine(home, ".local", "share", "mime", "packages");
        var appsDir = Path.Combine(home, ".local", "share", "applications");

        // Track whether we need to refresh caches
        bool mimeChanged    = false;
        bool desktopChanged = false;

        // 1 · MIME type declaration ──────────────────────────────────────────
        var mimeXmlPath = Path.Combine(mimeDir, "x-mogwai.xml");
        var mimeXml =
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
              <mime-type type="{MimeType}">
                <comment>MOGWAI Script</comment>
                <glob pattern="*{Extension}"/>
              </mime-type>
            </mime-info>
            """;

        if (!File.Exists(mimeXmlPath) ||
            !string.Equals(File.ReadAllText(mimeXmlPath), mimeXml, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(mimeDir);
            File.WriteAllText(mimeXmlPath, mimeXml);
            mimeChanged = true;
        }

        // 2 · .desktop entry ─────────────────────────────────────────────────
        var desktopPath = Path.Combine(appsDir, DesktopId);
        var desktopContent =
            $"""
            [Desktop Entry]
            Version=1.0
            Type=Application
            Name=MOGWAI CLI
            Comment=MOGWAI RPN scripting engine
            Exec="{exePath}" %f
            Icon={exePath}
            MimeType={MimeType};
            NoDisplay=true
            Categories=Development;
            """;

        if (!File.Exists(desktopPath) ||
            !File.ReadAllText(desktopPath).Contains(exePath, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(appsDir);
            File.WriteAllText(desktopPath, desktopContent);
            desktopChanged = true;
        }

        if (!mimeChanged && !desktopChanged)
            return; // Everything already up-to-date

        // 3 · Refresh system databases ───────────────────────────────────────
        if (mimeChanged)
            RunCommand("update-mime-database",
                       Path.Combine(home, ".local", "share", "mime"));

        if (desktopChanged)
        {
            RunCommand("xdg-mime", $"default {DesktopId} {MimeType}");
            RunCommand("update-desktop-database", appsDir);
        }

        Console.WriteLine($"[MOGWAI] {Extension} files are now associated with MOGWAI_CLI.");
    }

    // ── macOS (best-effort) ───────────────────────────────────────────────────

    [SupportedOSPlatform("macos")]
    private static void EnsureMacOSAssociation()
    {
        // Full association on macOS requires a proper .app bundle.
        // Without one, lsregister can register the bare executable with Launch
        // Services, which enables open(1) and some Finder integrations, but
        // icon overlays and reliable double-click may not work.

        var exePath = GetExePath();
        if (exePath is null)
        {
            Warn("Cannot determine executable path; file association skipped.");
            return;
        }

        const string lsRegister =
            "/System/Library/Frameworks/CoreServices.framework"
            + "/Versions/A/Frameworks/LaunchServices.framework"
            + "/Versions/A/Support/lsregister";

        if (!File.Exists(lsRegister))
        {
            Warn("lsregister not found; .mog association on macOS skipped.");
            return;
        }

        // Check current registrations to avoid redundant writes
        var dump = RunCommandOutput(lsRegister, "-dump");
        if (dump.Contains(exePath,  StringComparison.OrdinalIgnoreCase) &&
            dump.Contains(Extension, StringComparison.OrdinalIgnoreCase))
            return;

        RunCommand(lsRegister, $"-f \"{exePath}\"");

        Console.WriteLine(
            $"[MOGWAI] {Extension} registered with macOS Launch Services. " +
            "(Note: a .app bundle is required for full Finder integration.)");
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>Returns the absolute path of the running executable, or null.</summary>
    private static string? GetExePath()
    {
        var path = Environment.ProcessPath
                   ?? Process.GetCurrentProcess().MainModule?.FileName;
        return path is null ? null : Path.GetFullPath(path);
    }

    /// <summary>Runs an external command, ignoring its output and exit code.</summary>
    private static void RunCommand(string command, string arguments)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName               = command,
                Arguments              = arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            });
            proc?.WaitForExit();
        }
        catch (Exception ex)
        {
            Warn($"'{command}' failed: {ex.Message}");
        }
    }

    /// <summary>Runs a command and returns its stdout, or an empty string.</summary>
    private static string RunCommandOutput(string command, string arguments)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName               = command,
                Arguments              = arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            });
            return proc?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Warn(string message)
        => Console.WriteLine($"[MOGWAI] Warning: {message}");
}
