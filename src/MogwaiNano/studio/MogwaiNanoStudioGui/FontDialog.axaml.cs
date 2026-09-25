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
using Avalonia.Media;
using System.Linq;

namespace MogwaiNanoStudioGui;

public partial class FontDialog : Window
{
    public string? SelectedFontFamily { get; private set; }

    public double? SelectedFontSize { get; private set; }

    // Parameterless constructor required by the XAML loader.
    public FontDialog() : this(string.Empty, 14)
    {
    }

    public FontDialog(string currentFontFamily, double currentFontSize)
    {
        InitializeComponent();
        Icon = AppIcon.Icon;

        // Only the fonts actually installed on THIS machine are listed —
        // guarantees the choice is always valid, on any platform, rather
        // than offering names that might not exist here.
        var families = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        FontFamilyCombo.ItemsSource = families;

        // currentFontFamily may be a fallback list (e.g.
        // "Consolas,Cascadia Code,monospace") rather than a single real
        // name — match against its first entry only.
        var firstCurrentName = currentFontFamily.Split(',').FirstOrDefault()?.Trim();
        FontFamilyCombo.SelectedItem = families.FirstOrDefault(f => f == firstCurrentName) ?? families.FirstOrDefault();

        FontSizeBox.Text = currentFontSize.ToString();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (FontFamilyCombo.SelectedItem is string family)
            SelectedFontFamily = family;

        if (double.TryParse(FontSizeBox.Text, out var size) && size > 0)
            SelectedFontSize = size;

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
