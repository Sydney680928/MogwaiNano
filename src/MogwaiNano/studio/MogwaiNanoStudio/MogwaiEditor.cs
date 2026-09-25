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

using MOGWAI.Engine;
using System.Text;
using Terminal.Gui;

namespace MogwaiNanoStudio
{
    /// <summary>
    /// Éditeur de code MOGWAI en mode TUI (Terminal.Gui 1.x).
    /// Pas de MenuBar — tout par raccourcis clavier pour éviter les conflits
    /// avec AltGr sur AZERTY sous Windows Terminal.
    /// DOIT être ouvert depuis le thread principal.
    /// </summary>
    internal class MogwaiEditor
    {
        private readonly MogwaiEngine   _engine;
        private readonly EngineDelegate _delegate;

        // ─── État persistant entre les sessions edit ──────────────────────────

        /// <summary>Code en cours de saisie. Survit à la fermeture de l'éditeur.</summary>
        private string _sessionCode = string.Empty;

        /// <summary>Contenu du dernier enregistrement (référence pour dirty check).</summary>
        private string _savedText = string.Empty;

        /// <summary>Chemin du fichier courant. Vide = non sauvegardé.</summary>
        private string _filename = string.Empty;

        // ─── Propriétés publiques ─────────────────────────────────────────────

        /// <summary>True si le code en session diffère du dernier enregistrement.</summary>
        public bool HasUnsavedChanges => _sessionCode != _savedText;

        public string? PendingRunCode { get; private set; }

        public MogwaiEditor(MogwaiEngine engine, EngineDelegate engineDelegate)
        {
            _engine   = engine;
            _delegate = engineDelegate;
        }

        private static string Normalize(string s) => s.Replace("\r\n", "\n");

        public void Open()
        {
            PendingRunCode = null;

            var cmdBar = new Label("  Ctrl+N New   Ctrl+O Open   Ctrl+W Save   Ctrl+A Save as   F5 Run   Ctrl+Q Quit")
            {
                X     = 0,
                Y     = Pos.AnchorEnd(2),
                Width = Dim.Fill(),
                ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
                    Focus  = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
                },
            };

            var hintBar = new Label("")
            {
                X     = 0,
                Y     = Pos.AnchorEnd(1),
                Width = Dim.Fill(),
                ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
                    Focus  = new Terminal.Gui.Attribute(Color.Black, Color.Gray),
                },
            };

            const int gutterWidth = 6; // "9999 │" = 6 chars

            var lineNumView = new TextView
            {
                X        = 0,
                Y        = 0,
                Width    = gutterWidth,
                Height   = Dim.Fill(),
                ReadOnly = true,
                WordWrap = false,
                CanFocus = false,
                ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                    Focus  = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                },
            };

            var textView = new EditorTextView
            {
                X        = gutterWidth,
                Y        = 0,
                Width    = Dim.Fill(),
                Height   = Dim.Fill(),
                WordWrap = false,
                Text     = _sessionCode,
                ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                    Focus  = new Terminal.Gui.Attribute(Color.White, Color.Black),
                },
            };

            var window = new Window(BuildTitle(_sessionCode))
            {
                X      = 0,
                Y      = 0,
                Width  = Dim.Fill(),
                Height = Dim.Fill() - 2,
                ColorScheme = new ColorScheme
                {
                    Normal    = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                    Focus     = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                    HotNormal = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                    HotFocus  = new Terminal.Gui.Attribute(Color.Gray, Color.Black),
                },
            };
            window.Add(lineNumView, textView);

            textView.OnNew    = () => DoNew(textView, lineNumView, window);
            textView.OnOpen   = () => { if (ConfirmSave(textView)) DoOpen(textView, lineNumView, window); };
            textView.OnSave   = () => { DoSave(textView); window.Title = BuildTitle(GetText(textView)); };
            textView.OnSaveAs = () => { DoSaveAs(textView); window.Title = BuildTitle(GetText(textView)); };
            textView.OnRun    = () => DoRun(textView);
            textView.OnQuit   = () => { if (ConfirmSave(textView)) Application.RequestStop(); };

            ConsoleDriver? driver = null;
            try
            {
                var driverType = typeof(Application).Assembly
                    .GetType("Terminal.Gui.WindowsDriver");
                if (driverType != null)
                    driver = (ConsoleDriver?)Activator.CreateInstance(driverType);
            }
            catch { }

            Application.Init(driver);
            Application.Top.Add(cmdBar, hintBar, window);

            RefreshLineNumbers(lineNumView, textView);

            object? timerToken = null;
            timerToken = Application.MainLoop.AddTimeout(
                TimeSpan.FromMilliseconds(50),
                _ =>
                {
                    if (lineNumView.TopRow != textView.TopRow)
                    {
                        lineNumView.TopRow = textView.TopRow;
                        lineNumView.SetNeedsDisplay();
                    }

                    RefreshLineNumbers(lineNumView, textView);

                    var newTitle = BuildTitle(GetText(textView));
                    if (window.Title != newTitle)
                        window.Title = newTitle;

                    var hint = $"  Ln {textView.CurrentRow + 1}  Col {textView.CurrentColumn + 1}" +
                               (_filename == string.Empty ? "   [untitled]" : $"   {_filename}");
                    if (hintBar.Text.ToString() != hint)
                        hintBar.Text = hint;

                    return true;
                });

            Application.Top.KeyPress += (e) =>
            {
                switch (e.KeyEvent.Key)
                {
                    case Key.F5:
                        DoRun(textView);
                        e.Handled = true;
                        break;

                    case Key.CtrlMask | Key.W:
                        DoSave(textView);
                        window.Title = BuildTitle(GetText(textView));
                        e.Handled = true;
                        break;

                    case Key.CtrlMask | Key.Q:
                        if (ConfirmSave(textView))
                            Application.RequestStop();
                        e.Handled = true;
                        break;
                }
            };

            Application.Run();

            Application.MainLoop.RemoveTimeout(timerToken);
            _sessionCode = GetText(textView);
            Application.Shutdown();
        }

        private static string GetText(TextView tv) =>
            Normalize(tv.Text.ToString() ?? string.Empty);

        private static void RefreshLineNumbers(TextView lineNumView, TextView textView)
        {
            var text  = GetText(textView);
            var count = text.Split('\n').Length;
            var sb    = new StringBuilder(count * 7);

            for (int i = 1; i <= count; i++)
                sb.AppendLine($"{i,4} │");

            var newText = sb.ToString();
            if (lineNumView.Text.ToString() != newText)
                lineNumView.Text = newText;
        }

        private string BuildTitle(string currentCode)
        {
            var name  = _filename == string.Empty ? "[untitled]" : Path.GetFileName(_filename);
            var dirty = currentCode != _savedText ? " ●" : string.Empty;
            return $"MOGWAI NANO Editor — {name}{dirty}";
        }

        private void DoNew(TextView textView, TextView lineNumView, Window window)
        {
            if (!ConfirmSave(textView)) return;

            textView.Text = string.Empty;
            _savedText    = string.Empty;
            _sessionCode  = string.Empty;
            _filename     = string.Empty;
            RefreshLineNumbers(lineNumView, textView);
            window.Title  = BuildTitle(string.Empty);
        }

        private void DoRun(TextView textView)
        {
            _sessionCode = GetText(textView);

            PendingRunCode = _sessionCode;
            Application.RequestStop();
        }

        private void DoOpen(TextView textView, TextView lineNumView, Window window)
        {
            var dlg = new OpenDialog("Open", "")
            {
                DirectoryPath           = _engine.ProgramsDirectory,
                AllowsMultipleSelection = false,
            };

            Application.Run(dlg);

            if (!dlg.Canceled && dlg.FilePaths.Count > 0)
            {
                try
                {
                    var content   = Normalize(File.ReadAllText(dlg.FilePaths[0]));
                    _filename     = dlg.FilePaths[0];
                    _savedText    = content;
                    _sessionCode  = content;
                    textView.Text = content;
                    RefreshLineNumbers(lineNumView, textView);
                    window.Title  = BuildTitle(content);
                }
                catch
                {
                    MessageBox.ErrorQuery("Open", "Unable to open the file!", "OK");
                }
            }
        }

        private bool DoSave(TextView textView)
        {
            if (_filename == string.Empty)
                return DoSaveAs(textView);

            var content = GetText(textView);
            try
            {
                File.WriteAllText(_filename, content);
                _savedText   = content;
                _sessionCode = content;
                return true;
            }
            catch
            {
                MessageBox.ErrorQuery("Save", "Unable to save the file!", "OK");
            }

            return false;
        }

        private bool DoSaveAs(TextView textView)
        {
            var dlg = new SaveDialog("Save as...", "")
            {
                DirectoryPath    = _engine.ProgramsDirectory,
                FilePath         = _filename == string.Empty ? "new program.mog" : _filename,
                AllowedFileTypes = [".mog"],
            };

            Application.Run(dlg);

            if (!dlg.Canceled)
            {
                var path = dlg.FilePath.ToString();
                if (path != null)
                {
                    var content = GetText(textView);
                    try
                    {
                        if (File.Exists(path))
                        {
                            if (MessageBox.Query("Save",
                                    "This file already exists. Overwrite?", "Yes", "No") != 0)
                                return false;
                        }

                        File.WriteAllText(path, content);
                        _filename    = path;
                        _savedText   = content;
                        _sessionCode = content;
                        return true;
                    }
                    catch
                    {
                        MessageBox.ErrorQuery("Save", "Unable to save the file!", "OK");
                    }
                }
            }

            return false;
        }

        private bool ConfirmSave(TextView textView)
        {
            var current = GetText(textView);
            if (current == _savedText)
                return true;

            int r = MessageBox.Query("Save",
                "Modifications are not saved. Save?",
                "Yes", "No", "Cancel");

            return r switch
            {
                0 => DoSave(textView),
                1 => true,
                _ => false,
            };
        }

        private sealed class EditorTextView : TextView
        {
            public Action? OnNew;
            public Action? OnOpen;
            public Action? OnSave;
            public Action? OnSaveAs;
            public Action? OnRun;
            public Action? OnQuit;

            public override bool ProcessKey(KeyEvent keyEvent)
            {
                switch (keyEvent.Key)
                {
                    case Key.CtrlMask | Key.N: OnNew?.Invoke();    return true;
                    case Key.CtrlMask | Key.O: OnOpen?.Invoke();   return true;
                    case Key.CtrlMask | Key.W: OnSave?.Invoke();   return true;
                    case Key.CtrlMask | Key.A: OnSaveAs?.Invoke(); return true;
                    case Key.CtrlMask | Key.Q: OnQuit?.Invoke();   return true;
                    case Key.F5:               OnRun?.Invoke();    return true;
                    default:
                        return base.ProcessKey(keyEvent);
                }
            }
        }
    }
}
