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
using Avalonia.Interactivity;
using MOGWAI.Engine;
using System.Reflection;

namespace MogwaiNanoStudioGui;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        Icon = AppIcon.Icon;

        // Studio version: pulled from the assembly, never hardcoded — same
        // convention as the CLI Studio's Program.cs.
        var studioVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        StudioVersionText.Text = $"MOGWAI NANO STUDIO version {studioVersion}";
        StudioCopyrightText.Text = "(c) Stéphane SIBUE 2026";

        // Version + copyright of the MOGWAI engine itself: shown exactly as
        // provided by the runtime (RuntimePrompt), never reconstructed here.
        EngineInfoText.Text = MogwaiEngine.RuntimePrompt;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
