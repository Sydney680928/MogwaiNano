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

public partial class UnitsWindow : Window
{
    public UnitsWindow()
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
                var result = await AppGlobal.NanoRuntime.GetUnits();

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
                StatusText.Text = "Not connected — showing units available locally. Connect to a device to install one.";
            }

            UnitsGrid.ItemsSource = candidates;
        }
        finally
        {
            Cursor = Cursor.Default;
        }
    }

    // A candidate is any .mog file directly under UnitsManager.UnitsFolder —
    // a unit's name is its file name, matching nano.units.install's own
    // convention (device-side, a unit is stored under the name it was sent
    // with).
    private static List<UnitCandidate> FindLocalCandidates()
    {
        var result = new List<UnitCandidate>();
        var root = UnitsManager.UnitsFolder;

        if (!Directory.Exists(root))
            return result;

        foreach (var file in Directory.GetFiles(root, "*.mog"))
        {
            result.Add(new UnitCandidate
            {
                Name = Path.GetFileName(file),
                LocalPath = file
            });
        }

        return result;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var connected = AppGlobal.NanoClient.IsConnected;
        var candidate = UnitsGrid.SelectedItem as UnitCandidate;

        InstallButton.IsEnabled = candidate != null && connected;
        PurgeButton.IsEnabled = candidate != null && connected && candidate.IsInstalled == true;
    }

    private async void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        if (UnitsGrid.SelectedItem is not UnitCandidate candidate)
            return;

        InstallButton.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            // Unlike a using (a folder of pre-compiled .pe files, copied
            // as-is), a unit is source code — InstallUnitAsync takes the
            // file's text content directly, not a path, since the device
            // itself only ever needs the parsed/canonical form of it.
            var code = await File.ReadAllTextAsync(candidate.LocalPath);
            var result = await AppGlobal.NanoRuntime.InstallUnitAsync(candidate.Name, code);

            var boolResult = AppGlobal.MogwaiEngine.StackPopBoolean();
            var success = !result.IsError && boolResult != null && boolResult.Value;

            StatusText.Text = success
                ? $"'{candidate.Name}' installed successfully."
                : $"Failed to install '{candidate.Name}'.";

            if (success)
                await LoadAsync();
        }
        finally
        {
            Cursor = Cursor.Default;
            InstallButton.IsEnabled = true;
        }
    }

    private async void OnPurgeClick(object? sender, RoutedEventArgs e)
    {
        if (UnitsGrid.SelectedItem is not UnitCandidate candidate)
            return;

        PurgeButton.IsEnabled = false;
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            var result = await AppGlobal.NanoRuntime.PurgeUnitAsync(candidate.Name);
            var boolResult = AppGlobal.MogwaiEngine.StackPopBoolean();
            var success = !result.IsError && boolResult != null && boolResult.Value;

            StatusText.Text = success
                ? $"'{candidate.Name}' removed from the device."
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
