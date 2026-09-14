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

namespace MogwaiNanoStudioGui;

public partial class InputDialog : Window
{
    // Parameterless constructor required by the XAML loader — the real
    // message is applied by the actual constructor below.
    public InputDialog() : this(string.Empty)
    {
    }

    public InputDialog(string message)
    {
        InitializeComponent();
        Icon = AppIcon.Icon;
        PromptText.Text = message;
        Opened += (_, _) => InputBox.Focus();
    }

    // Enter = confirm, Escape = cancel — neither one, nor closing via the
    // window's close button, ever returns null (see EngineDelegate.Prompt,
    // which always converts a null result to an empty string).
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(InputBox.Text ?? string.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(string.Empty);
            e.Handled = true;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(InputBox.Text ?? string.Empty);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(string.Empty);
    }
}
