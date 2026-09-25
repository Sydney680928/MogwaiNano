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

namespace MogwaiNanoStudioGui;

// Cancel is deliberately the first (default) value: if the dialog is closed
// via the window's close button rather than one of the three buttons,
// ShowDialog<UnsavedChangesChoice>() returns default(UnsavedChangesChoice) —
// which must be the safe "don't proceed" choice, not an accidental Save.
public enum UnsavedChangesChoice
{
    Cancel,
    Save,
    Discard
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
        Icon = AppIcon.Icon;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
