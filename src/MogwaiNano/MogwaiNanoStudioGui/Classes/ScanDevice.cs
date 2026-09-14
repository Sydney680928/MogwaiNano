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

namespace MogwaiNanoStudioGui.Classes;

// Represents a MOGWAI NANO device discovered during a network scan.
public class ScanDevice
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    // Maps to the "target" field of the scan protocol (e.g. "ESP32_REV3"),
    // not the generic "platform" field — consistent with the 4th column
    // already shown by the old MogwaiNanoRuntime.Select() in console mode.
    public string Platform { get; set; } = string.Empty;

    // Extra fields, not shown in the DataGrid (which only displays the 4
    // properties above), but needed to rebuild a full MOGRecord identical to
    // the one produced by MogwaiNanoClient.Scan() — used when this device is
    // picked via the dialog for nano.user.select.
    public string Session { get; set; } = string.Empty;
    public string GenericPlatform { get; set; } = string.Empty;
    public string Oem { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;

    public override string ToString() => $"{Name} ({Platform}) - {IpAddress}";
}
