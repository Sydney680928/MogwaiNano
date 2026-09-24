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

// Ensures the usings shipped with this Studio release are ready to install
// without the user ever having to download or unzip anything themselves.
//
// Each Studio version gets its own folder under %APPDATA% — never shared
// across versions, and never overwritten once created. This trades a small
// amount of disk space (each version keeps its own copy) for a guarantee
// that upgrading the Studio can never silently wipe out a using a user
// hand-edited or replaced in an older version's folder — see the folder
// path itself for exactly how a version resolves to a path.
public static class UsingsManager
{
    // Matches <EmbeddedResource Include="Resources\usings.zip" /> in the
    // .csproj, combined with the project's default namespace and the
    // folder it lives in — .NET's standard embedded-resource naming
    // scheme (folder separators become dots). If extraction silently does
    // nothing, this is the first thing to double-check — call
    // Assembly.GetExecutingAssembly().GetManifestResourceNames() to see
    // the exact name actually generated at build time.
    private const string EmbeddedResourceName = "MogwaiNanoStudioGui.Resources.usings.zip";

    // %APPDATA%\MogwaiNanoStudioGui\usings\<version>\
    public static string UsingsFolder
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
                "usings",
                versionFolder);
        }
    }

    // Call once at startup, before anything tries to list or install
    // usings. Extraction failures are logged and swallowed rather than
    // shown to the user or blocking startup — this is a convenience, not
    // something the app's ability to run should ever depend on; a user can
    // still point nano.usings.install at a manually-obtained folder if
    // this never runs successfully.
    public static void EnsureExtracted()
    {
        var folder = UsingsFolder;

        if (Directory.Exists(folder))
            return;   // already extracted for this exact Studio version

        try
        {
            Directory.CreateDirectory(folder);

            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);

            if (resourceStream == null)
            {
                Debug.WriteLine($"Embedded usings resource '{EmbeddedResourceName}' not found — skipping extraction.");
                return;
            }

            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.Combine(folder, entry.FullName);

                // Directory entries in a zip have an empty Name (just a
                // trailing slash in FullName) — create the directory and
                // move on, nothing to extract.
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
            Debug.WriteLine($"Failed to extract embedded usings: {ex.Message}");
        }
    }
}
