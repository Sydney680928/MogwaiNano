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
using MogwaiNanoStudioGui.Classes;
using System.Threading.Tasks;

namespace MogwaiNanoStudioGui;

public partial class ScanDevicesWindow : Window
{
    // The device chosen by the user, or null if they canceled/closed without picking one
    public ScanDevice? SelectedDevice { get; private set; }

    public ScanDevicesWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Icon;
        Opened += async (_, _) => await RescanAsync();
    }

    private async void OnRescanClick(object? sender, RoutedEventArgs e) => await RescanAsync();

    private async Task RescanAsync()
    {
        RescanButton.IsEnabled = false;
        ConnectSelectedButton.IsEnabled = false;
        DevicesGrid.ItemsSource = null;
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            // The scan blocks the calling thread for ~2s (UDP loop): run it
            // off the UI thread so the window doesn't freeze during that time.
            var devices = await Task.Run(() => AppGlobal.NanoClient.ScanDevices());
            DevicesGrid.ItemsSource = devices;
        }
        finally
        {
            Cursor = Cursor.Default;
            RescanButton.IsEnabled = true;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ConnectSelectedButton.IsEnabled = DevicesGrid.SelectedItem is ScanDevice;
    }

    private void OnConnectSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (DevicesGrid.SelectedItem is ScanDevice device)
        {
            SelectedDevice = device;
            Close();
        }
    }

    // Double-clicking a row is a shortcut for "select then Connect"
    private void OnDeviceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DevicesGrid.SelectedItem is ScanDevice device)
        {
            SelectedDevice = device;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        SelectedDevice = null;
        Close();
    }
}
