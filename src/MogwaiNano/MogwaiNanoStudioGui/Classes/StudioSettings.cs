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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MogwaiNanoStudioGui.Classes;

// Persisted Studio UI settings (splitter position, etc.) — stored as JSON in
// the user's application data folder. Designed to grow: adding a property
// here is enough to make it persist.
public class StudioSettings
{
    public double OutputPanelHeight { get; set; } = 220;

    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 700;

    // Nullable: null means "never saved yet" — in that case the window keeps
    // whatever default startup position Avalonia/the OS picks, rather than
    // forcing a specific point that might not make sense on a different
    // monitor setup on first launch.
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }

    // "System" (follows the OS setting), "Light", or "Dark".
    public string ThemePreference { get; set; } = "System";

    // Kept as a fallback list (like the XAML default) rather than a single
    // name, so an unavailable font degrades gracefully to a generic
    // monospace one instead of failing outright — matters if this settings
    // file is ever copied across machines/platforms.
    public string EditorFontFamily { get; set; } = "Consolas,Cascadia Code,monospace";
    public double EditorFontSize { get; set; } = 14;

    // Most-recently-used file paths, most recent first — capped at 5 entries
    // (see MainWindow.AddRecentFile).
    public List<string> RecentFiles { get; set; } = new();

    private static string SettingsFilePath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MogwaiNanoStudioGui");

            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }
    }

    public static StudioSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<StudioSettings>(json);

                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // missing, corrupted, or unreadable file: fall back to defaults
        }

        return new StudioSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // best-effort: a failed save shouldn't crash the app
        }
    }
}
