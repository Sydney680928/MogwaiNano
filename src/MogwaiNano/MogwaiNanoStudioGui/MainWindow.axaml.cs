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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using MogwaiNanoStudioGui.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace MogwaiNanoStudioGui;

public partial class MainWindow : Window
{
    // File filter for MOGWAI NANO scripts
    private static readonly FilePickerFileType MogFileType = new("MOGWAI NANO scripts")
    {
        Patterns = new[] { "*.mog" }
    };

    // The currently open file, if any (null until something has been
    // opened/saved — in which case "Save" behaves like "Save As")
    private IStorageFile? _currentFile;

    // Debug messages for MOGWAI (desktop engine) and NANO (device), each in
    // its own ObservableCollection/ListBox
    private readonly ObservableCollection<string> _mogwaiDebugMessages = new();
    private readonly ObservableCollection<string> _nanoDebugMessages = new();

    // History of commands typed into the command line (Console MOGWAI),
    // navigable with the Up/Down arrows like a real terminal
    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;

    // Persisted UI settings (splitter position, etc.)
    private readonly StudioSettings _settings;

    // Last known position/size while WindowState was Normal — tracked
    // continuously rather than read only at Closing time, since Width/Height/
    // Position would otherwise reflect the maximized/minimized bounds if the
    // window happens to be in that state when it closes.
    private PixelPoint? _lastNormalPosition;
    private Size? _lastNormalSize;

    // Set whenever the editor's content changes; cleared on New/Open/Save
    // (successful) — used to warn before an action that would lose it.
    private bool _hasUnsavedChanges;

    // Assigns Editor.Text programmatically (New/Open/recent files) without
    // marking the result as an unsaved change.
    //
    // Two things had to be combined here, not just one: unsubscribing
    // OnEditorTextChanged blocks it if TextChanged fires synchronously
    // right when Text is set — but if it's actually deferred (queued on the
    // dispatcher rather than fired immediately), resubscribing right after
    // (still on the same synchronous call stack) happens *before* that
    // queued notification ever runs, so it still fires afterward and
    // overrides a reset done directly here. Posting the reset instead
    // queues it onto the same dispatcher, after whatever the assignment
    // itself already queued — so it always ends up running last, however
    // TextChanged actually fires.
    private void SetEditorText(string text)
    {
        Editor.TextChanged -= OnEditorTextChanged;
        Editor.Text = text;
        Editor.TextChanged += OnEditorTextChanged;

        Dispatcher.UIThread.Post(() => _hasUnsavedChanges = false);
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e) => _hasUnsavedChanges = true;

    // Set right before deliberately re-closing the window after the user
    // confirmed they want to proceed despite unsaved changes — lets the
    // Closing handler skip re-asking on that second attempt.
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Icon = AppIcon.Icon;
        MogwaiDebugOutput.ItemsSource = _mogwaiDebugMessages;
        NanoDebugOutput.ItemsSource = _nanoDebugMessages;

        // Lets EngineDelegate.Prompt() open modal dialogs
        // (console.input/console.prompt) attached to this window.
        AppGlobal.EngineDelegate.OwnerWindow = this;

        _settings = StudioSettings.Load();
        ApplyTheme(_settings.ThemePreference);
        RefreshRecentFilesMenu();

        Editor.FontFamily = new FontFamily(_settings.EditorFontFamily);
        Editor.FontSize = _settings.EditorFontSize;

        // Stored as a named method (not a plain lambda) so it can be
        // temporarily unsubscribed — see SetEditorText — around programmatic
        // changes to Editor.Text (New/Open/recent files), rather than
        // relying on TextChanged firing synchronously right when Text is
        // set, which turned out not to be a safe assumption.
        Editor.TextChanged += OnEditorTextChanged;

        // Window size/position — restored from the last normal (not
        // maximized/minimized) bounds. No saved position yet on first run:
        // leave it to Avalonia/the OS's own default startup placement rather
        // than forcing a point that might not make sense on a different
        // monitor setup.
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        // Applied on Opened rather than here in the constructor — setting
        // Position before the native window (and its decorations) is fully
        // realized has been observed to interact poorly with how Avalonia
        // reports/applies window coordinates on some platforms.
        //
        // Known limitation: on Windows, the restored position can land a
        // few pixels lower than where it was saved from (see
        // AvaloniaUI/Avalonia#8161 for the same class of issue on Linux) —
        // a fix attempt (measuring and correcting the offset at runtime)
        // didn't help and was reverted. Left as a plain restore for now;
        // revisit later if a real fix surfaces.
        //
        // Validated against the actually available screens before being
        // applied: a garbage or stale value (e.g. from a monitor that's no
        // longer connected) previously placed the window entirely off any
        // visible screen — the app was still genuinely running (visible in
        // the taskbar), just with a window nobody could ever see or reach.
        if (_settings.WindowX is int savedX && _settings.WindowY is int savedY)
        {
            Opened += (_, _) =>
            {
                var savedPosition = new PixelPoint(savedX, savedY);

                if (IsPositionOnAnyScreen(savedPosition))
                    Position = savedPosition;
            };
        }

        PositionChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
                _lastNormalPosition = e.Point;
        };

        PropertyChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal &&
                (e.Property == WidthProperty || e.Property == HeightProperty))
            {
                _lastNormalSize = new Size(Width, Height);
            }
        };

        // Row 4 = the output panel row (Menu=0, Status bar=1, Editor=2,
        // GridSplitter=3, Output panel=4). RowDefinition doesn't support
        // x:Name in Avalonia (unlike WPF) — so we go through its index in
        // the named parent Grid's collection instead.
        var outputRow = RootGrid.RowDefinitions[4];
        outputRow.Height = new GridLength(_settings.OutputPanelHeight);

        Closing += async (_, e) =>
        {
            // Persisted regardless of whether the close actually goes
            // through below — reflects the window's current state either way.
            _settings.OutputPanelHeight = outputRow.Height.Value;

            // Falls back to the current bounds if the window was somehow
            // never seen in its Normal state (e.g. maximized immediately on
            // first launch) — but normally reflects the last genuinely
            // "restored" bounds, not whatever the maximized/minimized state
            // currently reports.
            var position = _lastNormalPosition ?? Position;
            _settings.WindowX = position.X;
            _settings.WindowY = position.Y;

            var size = _lastNormalSize ?? new Size(Width, Height);
            _settings.WindowWidth = size.Width;
            _settings.WindowHeight = size.Height;

            _settings.Save();

            if (_closeConfirmed || !_hasUnsavedChanges)
                return;

            // Pause the close while asking — Closing itself can't await a
            // dialog result directly, so we cancel here and, if the user
            // decides to proceed, close again afterward with _closeConfirmed
            // set so this check is skipped the second time.
            e.Cancel = true;

            if (await ConfirmDiscardUnsavedChangesAsync())
            {
                _closeConfirmed = true;
                Close();
            }
        };

        // Console + Debug MOGWAI (desktop engine, the Studio-side orchestration script)
        AppGlobal.EngineDelegate.ConsoleLineReceived += text => OnUiThread(() => WriteMogwaiConsoleLine(text));
        AppGlobal.EngineDelegate.ConsoleTextReceived += text => OnUiThread(() => WriteMogwaiConsole(text));
        AppGlobal.EngineDelegate.ConsoleClearRequested += () => OnUiThread(WriteMogwaiConsoleClear);
        AppGlobal.EngineDelegate.DebugMessageReceived += message => OnUiThread(() => AppendMogwaiDebugMessage(message));
        AppGlobal.EngineDelegate.DebugClearRequested += () => OnUiThread(() => _mogwaiDebugMessages.Clear());

        // console.show — switches to the Console MOGWAI tab from MOGWAI code
        AppGlobal.EngineDelegate.ConsoleShowRequested += () => OnUiThread(() => OutputTabControl.SelectedItem = ConsoleMogwaiTab);

        // nano.console.show / nano.debug.show / nano.console.clear /
        // nano.debug.clear — extended primitives acting on the device-side
        // Console/Debug NANO tabs, from MOGWAI code.
        AppGlobal.EngineDelegate.NanoConsoleShowRequested += () => OnUiThread(() => OutputTabControl.SelectedItem = ConsoleNanoTab);
        AppGlobal.EngineDelegate.NanoDebugShowRequested += () => OnUiThread(() => OutputTabControl.SelectedItem = DebugNanoTab);
        AppGlobal.EngineDelegate.NanoConsoleClearRequested += () => OnUiThread(() => ConsoleNanoOutput.Text = string.Empty);
        AppGlobal.EngineDelegate.NanoDebugClearRequested += () => OnUiThread(() => _nanoDebugMessages.Clear());

        // The result is displayed here, on the engine's ProgramEnded signal —
        // rather than right after RunAsync returns — to guarantee it appears
        // after every ConsolePrintLn/ConsolePrint of the program it's
        // concluding. The newline preceding the result is baked into the
        // SAME string (a single call), rather than sent as a separate call
        // beforehand — an inconsistent ordering was observed when the two
        // were posted as two separate actions.
        AppGlobal.EngineDelegate.ProgramEnded += result => OnUiThread(() =>
        {
            WriteMogwaiConsoleLine(Environment.NewLine + (result.ToString() ?? string.Empty));
            WriteMogwaiConsoleLine(string.Empty);
        });

        // Console + Debug NANO (device, once connected) — these events fire
        // from MogwaiNanoClient's network thread, never the UI thread: hence
        // the systematic use of OnUiThread below.
        AppGlobal.NanoRuntime.NanoPrintLn += text => OnUiThread(() => WriteNanoConsoleLine(text));
        AppGlobal.NanoRuntime.NanoPrint += text => OnUiThread(() => WriteNanoConsole(text));
        AppGlobal.NanoRuntime.NanoDebugWrite += message => OnUiThread(() => AppendNanoDebugMessage(message));
        AppGlobal.NanoRuntime.NanoDebugClear += () => OnUiThread(() => _nanoDebugMessages.Clear());
        AppGlobal.NanoRuntime.NanoConsoleClear += () => OnUiThread(() => ConsoleNanoOutput.Text = string.Empty);

        // Writes the device's final result to the Console NANO automatically
        // whenever a program running on it ends (PROGRAM.DID.END). The
        // leading newline is baked into the same string (one call), not
        // sent separately beforehand — same lesson learned from the MOGWAI
        // console's ProgramEnded handling: two separate calls risk an
        // inconsistent order against other concurrent output.
        AppGlobal.NanoRuntime.NanoProgramDidEnd += result => OnUiThread(() => WriteNanoConsoleLine(Environment.NewLine + result));

        // Connection state — reflects a connect/disconnect done from this UI
        // just as well as a nano.connect/nano.user.connect typed as MOGWAI
        // code: both the status bar AND the Device menu state.
        AppGlobal.EngineDelegate.NanoConnect += (name, address) => OnUiThread(() =>
        {
            StatusDot.Fill = Brushes.LimeGreen;
            StatusText.Text = $"{name} · {address}";
            UpdateDeviceMenuState(true);
        });

        AppGlobal.NanoClient.Disconnected += (_, _) => OnUiThread(() =>
        {
            StatusDot.Fill = Brushes.Gray;
            StatusText.Text = "Not connected";
            UpdateDeviceMenuState(false);
        });

        UpdateDeviceMenuState(false);
    }

    // True if the given point falls within the bounds of at least one
    // currently connected screen — guards against restoring a garbage or
    // stale position (e.g. from a monitor that's no longer connected).
    private bool IsPositionOnAnyScreen(PixelPoint position)
    {
        foreach (var screen in Screens.All)
        {
            if (screen.Bounds.Contains(position))
                return true;
        }

        return false;
    }

    // Keyboard shortcuts — the corresponding MenuItems only show the
    // InputGesture for display purposes (this project has no Command/
    // ICommand, just Click handlers): this is where the keys are actually
    // handled, directly reusing the same methods as the menu clicks.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    OnNewClick(this, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OnOpenClick(this, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    OnSaveClick(this, e);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            if (e.Key == Key.S)
            {
                OnSaveAsClick(this, e);
                e.Handled = true;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Key == Key.F5)
            {
                OnRunEditorClick(this, e);
                e.Handled = true;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            if (e.Key == Key.F5)
            {
                OnStopScriptClick(this, e);
                e.Handled = true;
            }
        }
    }

    // Guarantees the action runs on the UI thread, whether we're already on
    // it or not — needed since several of the events above come from the
    // network thread.
    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    // Caps applied to every console/debug output — without this, a
    // long-running program (hours of tasks starting/stopping, for example)
    // keeps growing these controls unbounded: not just a memory problem,
    // but each further append gets more expensive too (reassigning a
    // TextBox's Text reflows the whole thing), eventually stalling the UI
    // entirely even though the underlying program keeps running fine.
    private const int MaxDebugEntries = 500;
    private const int MaxConsoleLines = 500;

    // --- Debug MOGWAI (list, one entry per message) ---

    public void AppendMogwaiDebugMessage(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        var wasNearBottom = IsScrolledNearBottom(MogwaiDebugOutput);

        _mogwaiDebugMessages.Add(timestamped);

        while (_mogwaiDebugMessages.Count > MaxDebugEntries)
            _mogwaiDebugMessages.RemoveAt(0);

        if (wasNearBottom && _mogwaiDebugMessages.Count > 0)
            TryScrollIntoView(MogwaiDebugOutput, _mogwaiDebugMessages[^1]);
    }

    // --- Debug NANO (list, one entry per message) ---

    public void AppendNanoDebugMessage(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        var wasNearBottom = IsScrolledNearBottom(NanoDebugOutput);

        _nanoDebugMessages.Add(timestamped);

        while (_nanoDebugMessages.Count > MaxDebugEntries)
            _nanoDebugMessages.RemoveAt(0);

        if (wasNearBottom && _nanoDebugMessages.Count > 0)
            TryScrollIntoView(NanoDebugOutput, _nanoDebugMessages[^1]);
    }

    // Checked BEFORE adding a new item: if the user had scrolled up to read
    // older entries, a new message arriving shouldn't yank their view back
    // down to the bottom — only auto-follow if they were already there
    // (within a small tolerance for minor layout jitter). This also happens
    // to sidestep the ScrollIntoView layout race below in the common case,
    // since it's no longer called at all while the user is actively
    // scrolled away from the end.
    private static bool IsScrolledNearBottom(ListBox listBox)
    {
        if (listBox.Scroll is not { } scroll)
            return true; // not laid out yet — default to following

        const double tolerance = 20;
        return scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - tolerance;
    }

    // ScrollIntoView triggers a real layout pass internally. If that happens
    // to overlap with a layout pass from the user's own manual scrolling
    // (rapid new entries arriving while they're scrolling through the list),
    // Avalonia's VirtualizingStackPanel can throw "Invalid Arrange rectangle"
    // — a known class of fragility in its virtualization/viewport handling,
    // not something we can fix on our side. Rather than let a rare layout
    // race crash the whole app, we swallow it here: worst case, the list
    // doesn't auto-scroll for that one message.
    private static void TryScrollIntoView(ListBox listBox, string item)
    {
        try
        {
            listBox.ScrollIntoView(item);
        }
        catch (InvalidOperationException)
        {
        }
    }

    // --- Console MOGWAI (desktop engine output — command-line commands AND
    // code run from the editor share this same output) ---

    public void WriteMogwaiConsoleLine(string text)
    {
        SetConsoleText(ConsoleMogwaiOutput, (ConsoleMogwaiOutput.Text ?? string.Empty) + text + Environment.NewLine);
    }

    public void WriteMogwaiConsole(string text)
    {
        SetConsoleText(ConsoleMogwaiOutput, (ConsoleMogwaiOutput.Text ?? string.Empty) + text);
    }

    public void WriteMogwaiConsoleClear()
    {
        ConsoleMogwaiOutput.Text = string.Empty;
    }

    // --- Console NANO ('?'/console.print output from the device, streamed
    // once connected — the equivalent of what nano.user.view shows) ---

    public void WriteNanoConsoleLine(string text)
    {
        SetConsoleText(ConsoleNanoOutput, (ConsoleNanoOutput.Text ?? string.Empty) + text + Environment.NewLine);
    }

    public void WriteNanoConsole(string text)
    {
        SetConsoleText(ConsoleNanoOutput, (ConsoleNanoOutput.Text ?? string.Empty) + text);
    }

    // Applies newText to textBox, first trimming from the start down to
    // MaxConsoleLines if it's grown past that — keeps memory bounded and,
    // just as importantly, keeps the cost of each further append roughly
    // constant instead of growing with everything ever printed. Uses
    // IndexOf-based scanning rather than Split/Join to avoid allocating an
    // array of every line just to count them.
    private static void SetConsoleText(TextBox textBox, string newText)
    {
        var lineCount = 1;

        for (var i = 0; i < newText.Length; i++)
        {
            if (newText[i] == '\n')
                lineCount++;
        }

        if (lineCount > MaxConsoleLines)
        {
            var linesToSkip = lineCount - MaxConsoleLines;
            var index = -1;

            for (var i = 0; i < linesToSkip; i++)
            {
                index = newText.IndexOf('\n', index + 1);

                if (index < 0)
                    break;
            }

            if (index >= 0)
                newText = newText[(index + 1)..];
        }

        textBox.Text = newText;
        textBox.CaretIndex = newText.Length;
    }

    // --- Editor font, via the View menu ---

    private async void OnFontClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new FontDialog(_settings.EditorFontFamily, _settings.EditorFontSize);
        var confirmed = await dialog.ShowDialog<bool>(this);

        if (!confirmed)
            return;

        if (dialog.SelectedFontFamily is string family)
        {
            // Keeps a generic monospace fallback appended, so an unavailable
            // font (e.g. this settings file copied to another machine)
            // degrades gracefully rather than failing outright.
            _settings.EditorFontFamily = $"{family},monospace";
            Editor.FontFamily = new FontFamily(_settings.EditorFontFamily);
        }

        if (dialog.SelectedFontSize is double size)
        {
            _settings.EditorFontSize = size;
            Editor.FontSize = size;
        }

        _settings.Save();
    }

    // --- Theme, via the View menu ---

    private static void ApplyTheme(string preference)
    {
        Application.Current!.RequestedThemeVariant = preference switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // "System", or anything unrecognized
        };
    }

    // Applied immediately and saved right away (not just at Closing), so the
    // choice sticks even if the app is later closed abnormally.
    private void SetTheme(string preference)
    {
        _settings.ThemePreference = preference;
        ApplyTheme(preference);
        _settings.Save();
    }

    private void OnThemeSystemClick(object? sender, RoutedEventArgs e) => SetTheme("System");

    private void OnThemeLightClick(object? sender, RoutedEventArgs e) => SetTheme("Light");

    private void OnThemeDarkClick(object? sender, RoutedEventArgs e) => SetTheme("Dark");

    // --- Clearing, via the View menu ---

    private void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        OnClearMogwaiConsoleClick(sender, e);
        OnClearMogwaiDebugClick(sender, e);
        OnClearNanoConsoleClick(sender, e);
        OnClearNanoDebugClick(sender, e);
    }

    private void OnClearMogwaiConsoleClick(object? sender, RoutedEventArgs e) => WriteMogwaiConsoleClear();

    private void OnClearMogwaiDebugClick(object? sender, RoutedEventArgs e) => _mogwaiDebugMessages.Clear();

    private void OnClearNanoConsoleClick(object? sender, RoutedEventArgs e) => ConsoleNanoOutput.Text = string.Empty;

    private void OnClearNanoDebugClick(object? sender, RoutedEventArgs e) => _nanoDebugMessages.Clear();

    // --- Command line (Console MOGWAI) ---

    private async void OnCommandInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var command = CommandInput.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(command))
                return;

            _commandHistory.Add(command);
            _commandHistoryIndex = _commandHistory.Count;

            CommandInput.Text = string.Empty;
            await ExecuteMogwaiCommand(command);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_commandHistory.Count == 0)
                return;

            _commandHistoryIndex = Math.Max(0, _commandHistoryIndex - 1);
            SetCommandInputFromHistory();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_commandHistory.Count == 0)
                return;

            _commandHistoryIndex = Math.Min(_commandHistory.Count, _commandHistoryIndex + 1);
            SetCommandInputFromHistory();
            e.Handled = true;
        }
    }

    private void SetCommandInputFromHistory()
    {
        CommandInput.Text = _commandHistoryIndex < _commandHistory.Count
            ? _commandHistory[_commandHistoryIndex]
            : string.Empty;

        CommandInput.CaretIndex = CommandInput.Text?.Length ?? 0;
    }

    // Executes a command typed into the command line — echoes the command
    // in the console before its result, like a real REPL
    private async Task ExecuteMogwaiCommand(string command)
    {
        WriteMogwaiConsoleLine($"> {command}");
        WriteMogwaiConsoleLine(string.Empty);
        await RunMogwaiCode(command, isRepl: true);
    }

    // --- Editor: run its whole content as a MOGWAI script ---

    private async void OnRunEditorClick(object? sender, RoutedEventArgs e)
    {
        OutputTabControl.SelectedItem = ConsoleMogwaiTab;
        await RunMogwaiCode(Editor.Text ?? string.Empty, isRepl: false);
    }

    // Single real call site into the desktop MOGWAI engine, shared by the
    // command line (one instruction at a time, isRepl: true) and
    // "Run editor script" (the whole editor content, isRepl: false) — same
    // convention as the CLI Studio's Program.cs.
    //
    // No longer displays the result itself: that's now done via the
    // EngineDelegate.ProgramEnded event (see the constructor), to guarantee
    // correct ordering relative to the program's last internal
    // ConsolePrintLn/ConsolePrint calls. Here, only genuine C# exceptions
    // are handled (e.g. if RunAsync fails before even reaching ProgramEnd).
    private async Task RunMogwaiCode(string code, bool isRepl)
    {
        try
        {
            await AppGlobal.MogwaiEngine.RunAsync(code, isRepl);
        }
        catch (Exception ex)
        {
            WriteMogwaiConsoleLine(ex.Message);
            WriteMogwaiConsoleLine(string.Empty);
        }
    }

    // --- Files ---

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
            return;

        SetEditorText(string.Empty);
        _currentFile = null;
        UpdateTitle();
    }

    private async void OnExitClick(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
            return;

        // Skips the Closing handler's own re-check on this deliberate,
        // already-confirmed close.
        _closeConfirmed = true;
        Close();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a MOGWAI NANO script",
            AllowMultiple = false,
            FileTypeFilter = new[] { MogFileType, FilePickerFileTypes.All }
        });

        if (files.Count == 0)
            return;

        await LoadFileIntoEditorAsync(files[0]);
    }

    // Shared by "Open..." and by picking an entry from "Recent Files" — the
    // unsaved-changes check happens in each of those callers instead, since
    // "Recent Files" needs it too but arrives via a different path (see
    // OpenRecentFileAsync).
    private async Task LoadFileIntoEditorAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            SetEditorText(await reader.ReadToEndAsync());

            _currentFile = file;
            UpdateTitle();
            AddRecentFile(file);
        }
        catch (Exception ex)
        {
            // TODO: replace with a real user notification once a
            // message/toast mechanism is in place
            StatusText.Text = $"Failed to open file: {ex.Message}";
        }
    }

    // If there are unsaved changes: asks Save/Discard/Cancel and acts
    // accordingly. Returns true if it's fine to proceed with whatever would
    // otherwise discard the editor's current content (nothing to lose, the
    // user chose to discard, or a save just succeeded) — false if the
    // calling action should be aborted (Cancel, or the save itself didn't
    // go through, e.g. a canceled Save As).
    private async Task<bool> ConfirmDiscardUnsavedChangesAsync()
    {
        if (!_hasUnsavedChanges)
            return true;

        var dialog = new UnsavedChangesDialog();
        var choice = await dialog.ShowDialog<UnsavedChangesChoice>(this);

        switch (choice)
        {
            case UnsavedChangesChoice.Discard:
                return true;

            case UnsavedChangesChoice.Save:
                if (_currentFile is null)
                    await SaveAsAsync();
                else if (await WriteToFileAsync(_currentFile, Editor.Text ?? string.Empty))
                {
                    AddRecentFile(_currentFile);
                    _hasUnsavedChanges = false;
                }

                // Still true if the save above didn't actually go through
                // (e.g. Save As was itself canceled, or the write failed).
                return !_hasUnsavedChanges;

            default:
                return false;
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentFile is null)
        {
            await SaveAsAsync();
            return;
        }

        if (await WriteToFileAsync(_currentFile, Editor.Text ?? string.Empty))
        {
            AddRecentFile(_currentFile);
            _hasUnsavedChanges = false;
        }
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    private async Task SaveAsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MOGWAI NANO script",
            SuggestedFileName = _currentFile?.Name ?? "untitled.mog",
            DefaultExtension = "mog",
            FileTypeChoices = new[] { MogFileType, FilePickerFileTypes.All }
        });

        if (file is null)
            return;

        if (await WriteToFileAsync(file, Editor.Text ?? string.Empty))
        {
            _currentFile = file;
            _hasUnsavedChanges = false;
            UpdateTitle();
            AddRecentFile(file);
        }
    }

    private async Task<bool> WriteToFileAsync(IStorageFile file, string content)
    {
        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0); // in case we're overwriting a file longer than the new content
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
            return true;
        }
        catch (Exception ex)
        {
            // TODO: same as above, a real user notification would be better
            StatusText.Text = $"Failed to save file: {ex.Message}";
            return false;
        }
    }

    // --- Recent files, via the File menu ---

    // Adds/moves a file to the front of the recent-files list, capped at 5
    // entries. Files without a local path (e.g. a sandboxed/remote location
    // on some platforms) are silently skipped — nothing usable to remember.
    private void AddRecentFile(IStorageFile file)
    {
        var path = file.TryGetLocalPath();

        if (string.IsNullOrEmpty(path))
            return;

        _settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _settings.RecentFiles.Insert(0, path);

        if (_settings.RecentFiles.Count > 5)
            _settings.RecentFiles.RemoveRange(5, _settings.RecentFiles.Count - 5);

        _settings.Save();
        RefreshRecentFilesMenu();
    }

    private void RefreshRecentFilesMenu()
    {
        RecentFilesMenuItem.Items.Clear();

        if (_settings.RecentFiles.Count == 0)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem { Header = "(No recent files)", IsEnabled = false });
            return;
        }

        foreach (var path in _settings.RecentFiles)
        {
            var item = new MenuItem { Header = Path.GetFileName(path) };
            item.Click += async (_, _) => await OpenRecentFileAsync(path);
            RecentFilesMenuItem.Items.Add(item);
        }
    }

    private async Task OpenRecentFileAsync(string path)
    {
        if (!await ConfirmDiscardUnsavedChangesAsync())
            return;

        var file = await StorageProvider.TryGetFileFromPathAsync(path);

        if (file is null)
        {
            // No longer exists at that location — drop it rather than
            // leaving a dead entry in the menu.
            _settings.RecentFiles.Remove(path);
            _settings.Save();
            RefreshRecentFilesMenu();
            StatusText.Text = $"File not found: {path}";
            return;
        }

        await LoadFileIntoEditorAsync(file);
    }

    private void UpdateTitle()
    {
        Title = _currentFile is not null
            ? $"MOGWAI NANO Studio — {_currentFile.Name}"
            : "MOGWAI NANO Studio";
    }

    // --- Device connection ---

    // Enables/disables the Device menu commands based on connection state:
    // only "Connect..." stays usable while disconnected — everything else
    // (Disconnect, Reboot, Halt, the 3 info shortcuts) requires a connected device.
    private void UpdateDeviceMenuState(bool connected)
    {
        ConnectMenuItem.IsEnabled = !connected;
        DisconnectMenuItem.IsEnabled = connected;
        RebootMenuItem.IsEnabled = connected;
        HaltMenuItem.IsEnabled = connected;
        InfoMenuItem.IsEnabled = connected;
        MemoryMenuItem.IsEnabled = connected;
        StateMenuItem.IsEnabled = connected;
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ScanDevicesWindow();
        await dialog.ShowDialog(this);

        if (dialog.SelectedDevice is null)
            return; // canceled, or no device selected

        var device = dialog.SelectedDevice;
        StatusText.Text = $"Connecting to {device.Name}...";
        Cursor = new Cursor(StandardCursorType.Wait);

        try
        {
            // Connect() is blocking (opens a TCP socket): off the UI thread
            await Task.Run(() => AppGlobal.NanoClient.Connect(device.IpAddress, AppGlobal.TCP_PORT));

            StatusDot.Fill = Brushes.LimeGreen;
            StatusText.Text = $"{device.Name} · {device.IpAddress}";
            UpdateDeviceMenuState(true);
        }
        catch (Exception ex)
        {
            StatusDot.Fill = Brushes.Gray;
            StatusText.Text = $"Connection failed: {ex.Message}";
        }
        finally
        {
            Cursor = Cursor.Default;
        }
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        // StatusDot/StatusText/menu state are updated automatically via the
        // Disconnected event, already wired in the constructor.
        AppGlobal.NanoClient.Disconnect();
    }

    // Stops the MOGWAI script currently running on the Studio side — a
    // direct control on the engine, not code to have RunAsync interpret.
    private void OnStopScriptClick(object? sender, RoutedEventArgs e) => AppGlobal.MogwaiEngine.Halt();

    // Stops the program running on the DEVICE — nano.halt is a real MOGWAI
    // primitive (Studio-side), so it goes through the normal execution
    // path, as if it had been typed into the command line.
    private async void OnHaltDeviceProgramClick(object? sender, RoutedEventArgs e) => await ExecuteMogwaiCommand("nano.halt");

    private async void OnRebootClick(object? sender, RoutedEventArgs e) => await ExecuteMogwaiCommand("nano.reboot");

    // --- Quick info commands, via the Device menu ---
    // '?d' rather than '?': formats records/lists nicely (nano.info returns
    // one), rather than '?''s raw display.

    private async void OnNanoInfoClick(object? sender, RoutedEventArgs e) => await ExecuteMogwaiCommand("nano.info ?d");

    private async void OnNanoMemoryClick(object? sender, RoutedEventArgs e) => await ExecuteMogwaiCommand("nano.memory ?d");

    private async void OnNanoStateClick(object? sender, RoutedEventArgs e) => await ExecuteMogwaiCommand("nano.state ?d");

    // --- Studio mode (VS Code extension) ---

    // Faithfully reproduces the CLI Studio's "studio" command: starts the
    // desktop engine's network communication, disables the local editor
    // (everything goes through VS Code while this mode is active), and
    // waits for the socket server to stop on its own (VS Code disconnecting)
    // to give control back to the editor — the CLI exposes no manual stop,
    // so this menu doesn't offer one either for now.
    private async void OnStartStudioModeClick(object? sender, RoutedEventArgs e)
    {
        StudioModeMenuItem.IsEnabled = false;
        Editor.IsEnabled = false;

        WriteMogwaiConsoleLine("Studio mode started — waiting for the VS Code extension to connect...");
        WriteMogwaiConsoleLine(string.Empty);

        await AppGlobal.MogwaiEngine.StartNetworkCommunication();

        while (AppGlobal.MogwaiEngine.IsSocketServerRunning)
            await Task.Delay(250);

        Editor.IsEnabled = true;
        StudioModeMenuItem.IsEnabled = true;

        WriteMogwaiConsoleLine("Studio mode stopped — the local editor is usable again.");
        WriteMogwaiConsoleLine(string.Empty);
    }

    // --- About ---

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }
}
