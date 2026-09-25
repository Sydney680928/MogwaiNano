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
using Avalonia.Platform;
using System;
using System.Diagnostics;

namespace MogwaiNanoStudioGui;

// Loaded once and reused by every window (MainWindow and every dialog) —
// see each window's constructor, which sets `Icon = AppIcon.Icon;`.
//
// Nullable and loaded defensively: a static field initializer that throws
// (e.g. if Avalonia's icon decoder can't parse this particular .ico file's
// internal format) fails as a TypeInitializationException the very first
// time anything touches this class — which, being MainWindow's own
// constructor, silently prevented the whole app from ever showing its
// window. Falling back to no icon is far better than that.
public static class AppIcon
{
    public static readonly WindowIcon? Icon = TryLoadIcon();

    private static WindowIcon? TryLoadIcon()
    {
        try
        {
            return new WindowIcon(AssetLoader.Open(new Uri("avares://MogwaiNanoStudioGui/Assets/mogwai.ico")));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load application icon: {ex.Message}");
            return null;
        }
    }
}
