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
using MogwaiNanoStudio;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

AppGlobal.MogwaiEngine.AllowPrivatePrimitives = true;

AppGlobal.NanoRuntime.NanoDebugWrite += (message) =>
{
    if (AppGlobal.NanoRuntime.IsRunning && AppGlobal.NanoRuntime.DisplayMessages)
        Console.WriteLine($"[DBG] {message}");
};

AppGlobal.NanoRuntime.NanoPrint += (message) =>
{
    if (AppGlobal.NanoRuntime.IsRunning && AppGlobal.NanoRuntime.DisplayMessages)
        Console.Write(message);
};

AppGlobal.NanoRuntime.NanoPrintLn += (message) =>
{
    if (AppGlobal.NanoRuntime.IsRunning && AppGlobal.NanoRuntime.DisplayMessages)
        Console.WriteLine(message);
};

AppGlobal.EngineDelegate.NanoConnect += (name, address) =>
{
    Console.Title = $"{name} - {address}";
};

AppGlobal.NanoClient.Disconnected += (sender, eventArgs) =>
{
    Console.Title = "MOGWAI NANO STUDIO";
};

AppGlobal.NanoRuntime.NanoSendMessage += (message) =>
{
    Debug.WriteLine($"[NANO] {message}");
};  

if (args.Length > 0)
{
    Console.Title = "MOGWAI NANO STUDIO";

    try
    {
        var filename = Path.GetFileName(args[0]);
        var code = File.ReadAllText(args[0]);
        var result = await AppGlobal.MogwaiEngine.RunAsync(code, false);

        Console.WriteLine();
        Console.WriteLine(result);
        Console.ReadLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }

    return;
}

Console.Title = "MOGWAI NANO STUDIO";

Console.WriteLine("█   █  ███   ███  █   █  ███  ███   █   █  ███  █   █  ███ ");
Console.WriteLine("██ ██ █   █ █     █   █ █   █  █    ██  █ █   █ ██  █ █   █");
Console.WriteLine("█ █ █ █   █ █  ██ █ █ █ █████  █    █ █ █ █████ █ █ █ █   █");
Console.WriteLine("█   █ █   █ █   █ ██ ██ █   █  █    █  ██ █   █ █  ██ █   █");
Console.WriteLine("█   █  ███   ███  █   █ █   █ ███   █   █ █   █ █   █  ███ ");
Console.WriteLine();
Console.WriteLine($"MOGWAI NANO STUDIO version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}");
Console.WriteLine("(c) Stéphane SIBUE 2026");
Console.WriteLine();
Console.WriteLine(MogwaiEngine.RuntimePrompt);
Console.WriteLine();

var editor = new MogwaiEditor(AppGlobal.MogwaiEngine, AppGlobal.EngineDelegate);

Console.CancelKeyPress += (_, e) =>
{
    if (e.SpecialKey == ConsoleSpecialKey.ControlC)
    {
        if (AppGlobal.NanoRuntime.ViewMode)
        {
            AppGlobal.NanoRuntime.ExitViewModeRequested = true;
        }
        else
        {
            AppGlobal.MogwaiEngine.Halt();
        }

        e.Cancel = true;
    }
};

Console.WriteLine("Type 'help' for usage instructions.");
Console.WriteLine();

FileAssociationHelper.EnsureFileAssociation();

while (true)
{
    Console.WriteLine();
    Console.Write("MOGWAI NANO > ");

    var input = Console.ReadLine() ?? string.Empty;
    var cmd = input.Trim().ToUpper();

    if (cmd == "BYE")
    {
        // Avertissement si l'éditeur contient du code non sauvegardé

        if (editor.HasUnsavedChanges)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("The editor has unsaved changes.");
            Console.ResetColor();
            Console.Write("   Exit anyway? (y/N) ");

            var confirm = Console.ReadLine();

            if (!string.Equals(confirm?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                continue;
        }

        break;
    }
    else if (cmd == "EDIT")
    {
        do
        {
            editor.Open();

            if (editor.PendingRunCode is string codeToRun)
            {
                var r = await AppGlobal.MogwaiEngine.RunAsync(codeToRun, false);

                Console.WriteLine();
                Console.WriteLine(r);

                Console.WriteLine();
                Console.Write("Press any key to return to the editor... ");

                while (!Console.KeyAvailable)
                    await Task.Delay(100);

                _ = Console.ReadKey(true);

                Console.WriteLine();
            }

            Console.WriteLine();

        } while (editor.PendingRunCode != null);
    }
    else if (cmd == "STUDIO")
    {
        await AppGlobal.MogwaiEngine.StartNetworkCommunication();

        while (AppGlobal.MogwaiEngine.IsSocketServerRunning)
            await Task.Delay(250);

        Console.WriteLine();
        Console.WriteLine("Type 'help' for usage instructions.");
        Console.WriteLine();
    }
    else if (cmd == "HELP")
    {
        Console.WriteLine();
        Console.WriteLine("MOGWAI NANO STUDIO - Help");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  MogwaiNanoStudio <script.mog>  Run a script from a file.");
        Console.WriteLine();
        Console.WriteLine("Commands (in interactive mode):");
        Console.WriteLine("  edit                    Open the TUI code editor.");
        Console.WriteLine("  studio                  Start network communication with MOGWAI Studio or VS Code extension.");
        Console.WriteLine("  <file.mog> run          Run script <file.mog>");
        Console.WriteLine("  about                   Show information about MOGWAI_CLI.");
        Console.WriteLine("  help                    Show this help informations.");
        Console.WriteLine("  bye                     Exit the application.");
        Console.WriteLine();
    }
    else if (cmd == "ABOUT")
    {
        var appVersion = Assembly.GetExecutingAssembly()
          .GetName()
          .Version?.ToString(3) ?? "0.1.0";

        var mogwaiVersion = MOGWAI.Engine.MogwaiEngine.RuntimeVersion.ToString(4);

        Console.WriteLine();
        Console.WriteLine($"MOGWAI_CLI v{appVersion}");
        Console.WriteLine($"Built on MOGWAI v{mogwaiVersion}");
        Console.WriteLine();
    }
    else
    {
        try
        {
            var result = await AppGlobal.MogwaiEngine.RunAsync(input, true);

            Console.WriteLine();
            Console.WriteLine(result);

            if (result.IsError)
            {

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

