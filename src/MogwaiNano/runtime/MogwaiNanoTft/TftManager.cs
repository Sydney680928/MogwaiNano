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

using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;

namespace MogwaiNanoTft
{
    /// <summary>Hook used by the platform project to set the SPI pin functions (e.g. ESP32).</summary>
    public delegate void ConfigureSpiPinsHandler(int spiBus, int sclk, int mosi, int miso);

    /// <summary>
    /// SPI TFT display manager. A device has a single screen, shared by the main program and all tasks.
    /// </summary>
    public class TftManager
    {
        private static readonly object _lock = new object();
        private static Display _display;

        public static ConfigureSpiPinsHandler ConfigureSpiPins;

        public static Error TftInitError = new("TFT.1", "tft init error");
        public static Error TftIsClosedError = new("TFT.2", "tft already closed error");
        public static Error TftIsOpenedError = new("TFT.3", "tft already opened error");
        public static Error TftOperationError = new("TFT.4", "tft operation error");

        /// <summary>Current rotation of the screen (0 if no screen is open).</summary>
        public static int Rotation
        {
            get
            {
                lock (_lock)
                {
                    return _display != null ? _display.Rotation : 0;
                }
            }
        }

        private const int OpClear = 0;
        private const int OpSetRotation = 1;
        private const int OpPixel = 2;
        private const int OpHLine = 3;
        private const int OpVLine = 4;
        private const int OpLine = 5;
        private const int OpRect = 6;
        private const int OpFillRect = 7;
        private const int OpRoundRect = 8;
        private const int OpFillRoundRect = 9;
        private const int OpCircle = 10;
        private const int OpFillCircle = 11;
        private const int OpTriangle = 12;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        public static void Cleanup()
        {
            lock (_lock)
            {
                CloseInternal();
            }
        }

        private static void CloseInternal()
        {
            if (_display != null)
            {
                try { _display.SetBacklight(false); } catch (Exception) { }
                try { _display.Dispose(); } catch (Exception) { }
                _display = null;
            }
        }

        public static EvalResult PrimitiveTftInit(MogwaiNanoEngine engine, string name)
        {
            // record tft.init

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                lock (_lock)
                {
                    if (_display != null)
                        return EvalResult.Failure(engine, TftIsOpenedError, name);

                    TftConfig cfg;
                    var r = ParseConfig(engine, record, name, out cfg);

                    if (r.IsError)
                        return r;

                    SpiTftDriver driver = null;
                    Display display = null;

                    try
                    {
                        if (ConfigureSpiPins != null)
                            ConfigureSpiPins(cfg.SpiBus, cfg.Sclk, cfg.Mosi, cfg.Miso);

                        driver = new SpiTftDriver(cfg);
                        display = new Display(driver);
                        display.Initialize();

                        _display = display;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (display != null)
                                display.Dispose();
                            else if (driver != null)
                                driver.Dispose();
                        }
                        catch (Exception) { }

                        return EvalResult.Failure(engine, TftInitError, name, "failed to initialize tft display", ex.Message);
                    }
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTftClose(MogwaiNanoEngine engine, string name)
        {
            lock (_lock)
            {
                CloseInternal();
            }

            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Configuration record parsing
        // ------------------------------------------------------------------

        private class RecordReader
        {
            private readonly MOGRecord _rec;

            public string Problem;

            public RecordReader(MOGRecord rec)
            {
                _rec = rec;
            }

            public int Int(string key, bool required, int def)
            {
                if (Problem != null) return def;

                var item = _rec.GetItem(key);

                if (item == null)
                {
                    if (required) Problem = key + ": is mandatory";
                    return def;
                }

                var n = item as MOGNumber;

                if (n == null)
                {
                    Problem = key + ": must be a number";
                    return def;
                }

                return (int)n.Value;
            }

            public bool Bool(string key, bool def)
            {
                if (Problem != null) return def;

                var item = _rec.GetItem(key);

                if (item == null) return def;

                var b = item as MOGBoolean;

                if (b == null)
                {
                    Problem = key + ": must be a boolean";
                    return def;
                }

                return b.Value;
            }

            public string Str(string key)
            {
                if (Problem != null) return null;

                var item = _rec.GetItem(key);

                if (item == null) return null;

                var str = item as MOGString;
                if (str != null) return str.Value;

                var nm = item as MOGName;
                if (nm != null) return nm.Value;

                Problem = key + ": must be a string";
                return null;
            }

            public MOGData Data(string key)
            {
                if (Problem != null) return null;

                var item = _rec.GetItem(key);

                if (item == null) return null;

                var d = item as MOGData;

                if (d == null)
                    Problem = key + ": must be a data";

                return d;
            }

            public MOGList List(string key)
            {
                if (Problem != null) return null;

                var item = _rec.GetItem(key);

                if (item == null) return null;

                var l = item as MOGList;

                if (l == null)
                    Problem = key + ": must be a list";

                return l;
            }
        }

        private static EvalResult ParseConfig(MogwaiNanoEngine engine, MOGRecord rec, string name, out TftConfig cfg)
        {
            cfg = new TftConfig();

            var rd = new RecordReader(rec);

            cfg.Width = rd.Int("width", true, 0);
            cfg.Height = rd.Int("height", true, 0);
            cfg.SpiBus = rd.Int("spiBus", false, 1);
            cfg.Sclk = rd.Int("sclk", true, -1);
            cfg.Mosi = rd.Int("mosi", true, -1);
            cfg.Miso = rd.Int("miso", false, -1);
            cfg.Dc = rd.Int("dc", true, -1);
            cfg.Cs = rd.Int("cs", true, -1);
            cfg.Rst = rd.Int("rst", false, -1);
            cfg.Backlight = rd.Int("backlight", false, -1);
            cfg.ClockHz = rd.Int("clock", false, 40000000);
            cfg.XOffset = rd.Int("xOffset", false, 0);
            cfg.YOffset = rd.Int("yOffset", false, 0);
            cfg.Invert = rd.Bool("invert", false);
            cfg.Bgr = rd.Bool("bgr", false);
            cfg.CmdCaset = rd.Int("casetCommand", false, 0x2A);
            cfg.CmdRaset = rd.Int("rasetCommand", false, 0x2B);
            cfg.CmdRamwr = rd.Int("ramwrCommand", false, 0x2C);
            cfg.CmdMadctl = rd.Int("madctlCommand", false, 0x36);

            string controller = rd.Str("controller");
            MOGData init = rd.Data("init");
            MOGList rotations = rd.List("rotations");

            if (rd.Problem != null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, rd.Problem);

            if (cfg.Width <= 0 || cfg.Height <= 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "width: and height: must be greater than 0");

            if (init != null)
                cfg.InitTable = init.Items;

            if (rotations != null)
            {
                int[] values = new int[4];
                int count = 0;

                foreach (var item in rotations.Items)
                {
                    var n = item as MOGNumber;

                    if (n == null || count >= 4)
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "rotations: must be a list of 4 numbers");

                    values[count++] = (int)n.Value;
                }

                if (count != 4)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "rotations: must be a list of 4 numbers");

                cfg.Rotations = values;
            }

            if (controller != null)
            {
                if (controller.ToLower() == "st7789")
                {
                    if (cfg.InitTable == null)
                        cfg.InitTable = TftProfiles.St7789Init(cfg.Invert);

                    if (cfg.Rotations == null)
                        cfg.Rotations = TftProfiles.St7789Rotations();
                }
                else
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "controller: unknown controller (supported: \"st7789\"), use init: and rotations: for other chips");
                }
            }

            if (cfg.InitTable == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "init: is mandatory when controller: is not given");

            if (cfg.Rotations == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "rotations: is mandatory when controller: is not given");

            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        // Pops count numbers from the stack. v[0] is the deepest one.
        private static EvalResult PopInts(MogwaiNanoEngine engine, string name, int count, out int[] v)
        {
            v = null;

            var s = engine.StackSign(count);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            for (int i = 0; i < count; i++)
            {
                if (s[i] != typeof(MOGNumber))
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
            }

            v = new int[count];

            for (int i = count - 1; i >= 0; i--)
                v[i] = (int)(engine.StackPop() as MOGNumber).Value;

            return EvalResult.NoError;
        }

        private static EvalResult DrawNumbers(MogwaiNanoEngine engine, string name, int argCount, int op)
        {
            int[] v;
            var r = PopInts(engine, name, argCount, out v);

            if (r.IsError)
                return r;

            lock (_lock)
            {
                if (_display == null)
                    return EvalResult.Failure(engine, TftIsClosedError, name);

                try
                {
                    switch (op)
                    {
                        case OpClear: _display.Clear((ushort)v[0]); break;
                        case OpSetRotation: _display.SetRotation(v[0]); break;
                        case OpPixel: _display.DrawPixel(v[0], v[1], (ushort)v[2]); break;
                        case OpHLine: _display.DrawHLine(v[0], v[1], v[2], (ushort)v[3]); break;
                        case OpVLine: _display.DrawVLine(v[0], v[1], v[2], (ushort)v[3]); break;
                        case OpLine: _display.DrawLine(v[0], v[1], v[2], v[3], (ushort)v[4]); break;
                        case OpRect: _display.DrawRect(v[0], v[1], v[2], v[3], (ushort)v[4]); break;
                        case OpFillRect: _display.FillRect(v[0], v[1], v[2], v[3], (ushort)v[4]); break;
                        case OpRoundRect: _display.DrawRoundRect(v[0], v[1], v[2], v[3], v[4], (ushort)v[5]); break;
                        case OpFillRoundRect: _display.FillRoundRect(v[0], v[1], v[2], v[3], v[4], (ushort)v[5]); break;
                        case OpCircle: _display.DrawCircle(v[0], v[1], v[2], (ushort)v[3]); break;
                        case OpFillCircle: _display.FillCircle(v[0], v[1], v[2], (ushort)v[3]); break;
                        case OpTriangle: _display.DrawTriangle(v[0], v[1], v[2], v[3], v[4], v[5], (ushort)v[6]); break;
                    }
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, TftOperationError, name, ex.Message);
                }
            }

            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        public static EvalResult PrimitiveTftSetRotation(MogwaiNanoEngine engine, string name)
        {
            // n tft.setRotation
            return DrawNumbers(engine, name, 1, OpSetRotation);
        }

        public static EvalResult PrimitiveTftClear(MogwaiNanoEngine engine, string name)
        {
            // color tft.clear
            return DrawNumbers(engine, name, 1, OpClear);
        }

        public static EvalResult PrimitiveTftDrawPixel(MogwaiNanoEngine engine, string name)
        {
            // x y color tft.drawPixel
            return DrawNumbers(engine, name, 3, OpPixel);
        }

        public static EvalResult PrimitiveTftDrawHorizontalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len color tft.drawHorizontalLine
            return DrawNumbers(engine, name, 4, OpHLine);
        }

        public static EvalResult PrimitiveTftDrawVerticalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len color tft.drawVerticalLine
            return DrawNumbers(engine, name, 4, OpVLine);
        }

        public static EvalResult PrimitiveTftDrawLine(MogwaiNanoEngine engine, string name)
        {
            // x1 y1 x2 y2 color tft.drawLine
            return DrawNumbers(engine, name, 5, OpLine);
        }

        public static EvalResult PrimitiveTftDrawRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h color tft.drawRectangle
            return DrawNumbers(engine, name, 5, OpRect);
        }

        public static EvalResult PrimitiveTftDrawFilledRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h color tft.drawFilledRectangle
            return DrawNumbers(engine, name, 5, OpFillRect);
        }

        public static EvalResult PrimitiveTftDrawRoundRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h radius color tft.drawRoundRectangle
            return DrawNumbers(engine, name, 6, OpRoundRect);
        }

        public static EvalResult PrimitiveTftDrawFilledRoundRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h radius color tft.drawFilledRoundRectangle
            return DrawNumbers(engine, name, 6, OpFillRoundRect);
        }

        public static EvalResult PrimitiveTftDrawCircle(MogwaiNanoEngine engine, string name)
        {
            // x y radius color tft.drawCircle
            return DrawNumbers(engine, name, 4, OpCircle);
        }

        public static EvalResult PrimitiveTftDrawFilledCircle(MogwaiNanoEngine engine, string name)
        {
            // x y radius color tft.drawFilledCircle
            return DrawNumbers(engine, name, 4, OpFillCircle);
        }

        public static EvalResult PrimitiveTftDrawTriangle(MogwaiNanoEngine engine, string name)
        {
            // x1 y1 x2 y2 x3 y3 color tft.drawTriangle
            return DrawNumbers(engine, name, 7, OpTriangle);
        }

        public static EvalResult PrimitiveTftColor(MogwaiNanoEngine engine, string name)
        {
            // r g b tft.color -> color (RGB565)

            int[] v;
            var r = PopInts(engine, name, 3, out v);

            if (r.IsError)
                return r;

            engine.StackPush(new MOGNumber(engine, Display.Color565(v[0], v[1], v[2])));

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTftGetPixel(MogwaiNanoEngine engine, string name)
        {
            // x y tft.getPixel -> color

            int[] v;
            var r = PopInts(engine, name, 2, out v);

            if (r.IsError)
                return r;

            lock (_lock)
            {
                if (_display == null)
                    return EvalResult.Failure(engine, TftIsClosedError, name);

                engine.StackPush(new MOGNumber(engine, _display.GetPixel(v[0], v[1])));
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTftWidth(MogwaiNanoEngine engine, string name)
        {
            // tft.width -> n

            lock (_lock)
            {
                if (_display == null)
                    return EvalResult.Failure(engine, TftIsClosedError, name);

                engine.StackPush(new MOGNumber(engine, _display.Width));
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTftHeight(MogwaiNanoEngine engine, string name)
        {
            // tft.height -> n

            lock (_lock)
            {
                if (_display == null)
                    return EvalResult.Failure(engine, TftIsClosedError, name);

                engine.StackPush(new MOGNumber(engine, _display.Height));
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTftRefresh(MogwaiNanoEngine engine, string name)
        {
            // tft.refresh

            lock (_lock)
            {
                if (_display == null)
                    return EvalResult.Failure(engine, TftIsClosedError, name);

                try
                {
                    _display.Flush();
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, TftOperationError, name, ex.Message);
                }
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTftAutoRefresh(MogwaiNanoEngine engine, string name)
        {
            // true tft.autoRefresh

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean))
            {
                var on = engine.StackPop() as MOGBoolean;

                lock (_lock)
                {
                    if (_display == null)
                        return EvalResult.Failure(engine, TftIsClosedError, name);

                    _display.AutoFlush = on.Value;
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTftBacklight(MogwaiNanoEngine engine, string name)
        {
            // true tft.backlight

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean))
            {
                var on = engine.StackPop() as MOGBoolean;

                lock (_lock)
                {
                    if (_display == null)
                        return EvalResult.Failure(engine, TftIsClosedError, name);

                    try
                    {
                        _display.SetBacklight(on.Value);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, TftOperationError, name, ex.Message);
                    }
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTftDrawBitmap(MogwaiNanoEngine engine, string name)
        {
            // x y w h data tft.drawBitmap   (data = RGB565, 2 bytes per pixel, high byte first)

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var data = engine.StackPop() as MOGData;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                lock (_lock)
                {
                    if (_display == null)
                        return EvalResult.Failure(engine, TftIsClosedError, name);

                    try
                    {
                        _display.DrawBitmap((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value, data.Items);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, TftOperationError, name, ex.Message);
                    }
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTftDrawString(MogwaiNanoEngine engine, string name)
        {
            // x y text size fg bg tft.drawString   (bg = -1: transparent background)

            var s = engine.StackSign(6);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGString) && s[4] == typeof(MOGNumber) && s[5] == typeof(MOGNumber))
            {
                var bg = engine.StackPop() as MOGNumber;
                var fg = engine.StackPop() as MOGNumber;
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                lock (_lock)
                {
                    if (_display == null)
                        return EvalResult.Failure(engine, TftIsClosedError, name);

                    try
                    {
                        _display.DrawString((int)x.Value, (int)y.Value, text.Value, (ushort)(int)fg.Value, (int)bg.Value, (int)size.Value);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, TftOperationError, name, ex.Message);
                    }
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTftMeasureString(MogwaiNanoEngine engine, string name)
        {
            // text size tft.measureString -> width in pixels

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGString))
            {
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;

                lock (_lock)
                {
                    if (_display == null)
                        return EvalResult.Failure(engine, TftIsClosedError, name);

                    engine.StackPush(new MOGNumber(engine, _display.MeasureString(text.Value, (int)size.Value)));
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}