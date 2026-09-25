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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MogwaiNanoStudioGui.Classes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MogwaiNanoStudioGui;

public partial class UsingsWindow : Window
{
    public UsingsWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Icon;
        Opened += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Cursor = new Cursor(StandardCursorType.Wait);
        InstallButton.IsEnabled = false;
        PurgeButton.IsEnabled = false;

        try
        {
            var candidates = FindLocalCandidates();
            var connected = AppGlobal.NanoClient.IsConnected;

            if (connected)
            {
                var result = await AppGlobal.NanoRuntime.GetUsings();

                if (!result.IsError)
                {
                    var installedList = AppGlobal.MogwaiEngine.StackPopList();
                    var installedNames = installedList?.Items
                        .Select(item => (item as MOGWAI.Objects.MOGName)?.Value)
                        .Where(name => name != null)
                        .ToHashSet() ?? new HashSet<string?>();

                    foreach (var candidate in candidates)
                        candidate.IsInstalled = installedNames.Contains(candidate.Name);
                }

                StatusText.Text = "Connected — comparing against what's already installed on the device.";
            }
            else
            {
                StatusText.Text = "Not connected — showing usings available locally. Connect to a device to install one.";
            }

            UsingsGrid.ItemsSource = candidates;
        }
        finally
        {
            Cursor = Cursor.Default;
        }
    }

    // A candidate folder is anything directly under UsingsManager.UsingsFolder
    // that contains a manifest.txt — the same marker nano.usings.install's
    // own device-side counterpart relies on to recognize a real using.
    private static List<UsingCandidate> FindLocalCandidates()
    {
        var result = new List<UsingCandidate>();
        var root = UsingsManager.UsingsFolder;

        if (!Directory.Exists(root))
            return result;

        foreach (var dir in Directory.GetDirectories(root))
        {
            if (File.Exists(Path.Combine(dir, "manifest.txt")))
            {
                result.Add(new UsingCandidate
                {
                    Name = Path.GetFileName(dir),
                    LocalPath = dir
                });
            }
        }

        return result;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var connected = AppGlobal.NanoClient.IsConnected;
        var candidate = UsingsGrid.SelectedItem as UsingCandidate;

        InstallButton.IsEnabled = candidate != null && connected;
        PurgeButton.IsEnabled = candidate != null && connected && candidate.IsInstalled == true;
    }

    private async void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (UsingsGrid.SelectedItem is not UsingCandidate candidate)
            return;

        InstallButton.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            var result = await AppGlobal.NanoRuntime.InstallUsingAsync(candidate.Name, candidate.LocalPath);

            var boolResult = AppGlobal.MogwaiEngine.StackPopBoolean();
            var success = !result.IsError && boolResult != null && boolResult.Value;

            StatusText.Text = success
                ? $"'{candidate.Name}' installed successfully."
                : $"Failed to install '{candidate.Name}'.";

            if (success)
                await LoadAsync();   // full refresh — simpler and more robust than patching one row
        }
        finally
        {
            Cursor = Cursor.Default;
            InstallButton.IsEnabled = true;
        }
    }

    private async void OnPurgeClick(object? sender, RoutedEventArgs e)
    {
        if (UsingsGrid.SelectedItem is not UsingCandidate candidate)
            return;

        PurgeButton.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            var result = await AppGlobal.NanoRuntime.PurgeUsingsAsync(candidate.Name);
            var boolResult = AppGlobal.MogwaiEngine.StackPopBoolean();
            var success = !result.IsError && boolResult != null && boolResult.Value;

            // A using still loaded in memory keeps working until the next
            // reboot — purging only removes it from flash, preventing a
            // *future* mogwai.using on this name from succeeding.
            StatusText.Text = success
                ? $"'{candidate.Name}' removed from the device's flash. If it was already loaded, it keeps working until the next reboot."
                : $"Failed to remove '{candidate.Name}'.";

            if (success)
                await LoadAsync();
        }
        finally
        {
            Cursor = Cursor.Default;
            PurgeButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
