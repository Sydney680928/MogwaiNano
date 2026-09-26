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

// Represents a unit (.mog file) found locally, under
// UnitsManager.UnitsFolder — mirrors UsingCandidate, see its comments for
// why IsInstalled is a nullable bool.
public class UnitCandidate
{
    public string Name { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;

    public bool? IsInstalled { get; set; }

    public string Status => IsInstalled switch
    {
        true => "Installed",
        false => "Not installed",
        null => "Unknown (not connected)"
    };
}
