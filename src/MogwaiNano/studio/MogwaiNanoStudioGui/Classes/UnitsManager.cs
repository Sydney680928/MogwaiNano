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
using System.IO.Compression;
using System.Reflection;

namespace MogwaiNanoStudioGui.Classes;

// Ensures the units shipped with this Studio release are ready to install
// without the user ever having to download or unzip anything themselves.
// Mirrors UsingsManager exactly — see its comments for the reasoning behind
// the per-version folder and the "extract once, never touch again" policy.
public static class UnitsManager
{
    // Matches <EmbeddedResource Include="Resources\units.zip" /> in the
    // .csproj. If extraction silently does nothing, double-check this
    // against Assembly.GetExecutingAssembly().GetManifestResourceNames().
    private const string EmbeddedResourceName = "MogwaiNanoStudioGui.Resources.units.zip";

    // %APPDATA%\MogwaiNanoStudioGui\units\<version>\
    public static string UnitsFolder
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionFolder = version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "0.0.0";

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MogwaiNanoStudioGui",
                "units",
                versionFolder);
        }
    }

    // Call once at startup, before anything tries to list or install units.
    public static void EnsureExtracted()
    {
        var folder = UnitsFolder;

        if (Directory.Exists(folder))
            return;   // already extracted for this exact Studio version

        try
        {
            Directory.CreateDirectory(folder);

            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);

            if (resourceStream == null)
            {
                Debug.WriteLine($"Embedded units resource '{EmbeddedResourceName}' not found — skipping extraction.");
                return;
            }

            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.Combine(folder, entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract embedded units: {ex.Message}");
        }
    }
}
