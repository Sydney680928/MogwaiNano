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
using System.Diagnostics;
using System.Threading;

namespace MogwaiNanoTouch
{
    /// <summary>Hook used by the platform project to set the I2C pin functions (e.g. ESP32).</summary>
    public delegate void ConfigureI2cPinsHandler(int i2cBus, int sda, int scl);

    /// <summary>Returns the current rotation (0..3) of the screen the touch panel sits on.</summary>
    public delegate int RotationProvider();

    /// <summary>
    /// Touch manager. A device has a single touch panel, shared by the main program and all tasks.
    /// Events follow one finger at a time (the "primary" finger: the first one that touched the panel).
    /// </summary>
    public class TouchManager
    {
        public const string EVENT_TOUCH_DID_PRESS = "TOUCH_DID_PRESS";
        public const string EVENT_TOUCH_DID_MOVE = "TOUCH_DID_MOVE";
        public const string EVENT_TOUCH_DID_RELEASE = "TOUCH_DID_RELEASE";

        private const int PrimaryFinger = -2;

        private static readonly object _lock = new object();
        private static readonly TouchState _state = new TouchState();

        private static TouchChip _chip;
        private static TouchConfig _cfg;

        private static Thread _thread;
        private static bool _running;
        private static MogwaiNanoEngine _eventEngine;

        // Identifier of the finger followed by the events, -1 when nobody touches the panel.
        private static int _primaryId = -1;
        private static int _lastX;
        private static int _lastY;

        public static ConfigureI2cPinsHandler ConfigureI2cPins;
        public static RotationProvider GetRotation;

        public static Error TouchInitError = new("TOUCH.1", "touch init error");
        public static Error TouchIsClosedError = new("TOUCH.2", "touch already closed error");
        public static Error TouchIsOpenedError = new("TOUCH.3", "touch already opened error");
        public static Error TouchOperationError = new("TOUCH.4", "touch operation error");

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        public static void Cleanup()
        {
            StopPolling();

            lock (_lock)
            {
                CloseInternal();
            }
        }

        private static void CloseInternal()
        {
            if (_chip != null)
            {
                try { _chip.Dispose(); } catch (Exception) { }
                _chip = null;
            }

            _cfg = null;
            _state.Count = 0;
            _primaryId = -1;
        }

        private static void StopPolling()
        {
            Thread t;

            lock (_lock)
            {
                _running = false;
                t = _thread;
                _thread = null;
                _eventEngine = null;
            }

            if (t != null)
            {
                try { t.Join(); } catch (Exception) { }
            }
        }

        public static EvalResult PrimitiveTouchInit(MogwaiNanoEngine engine, string name)
        {
            // record touch.init

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                lock (_lock)
                {
                    if (_chip != null)
                        return EvalResult.Failure(engine, TouchIsOpenedError, name);

                    TouchConfig cfg;
                    var r = ParseConfig(engine, record, name, out cfg);

                    if (r.IsError)
                        return r;

                    TouchChip chip = null;

                    try
                    {
                        if (ConfigureI2cPins != null)
                            ConfigureI2cPins(cfg.I2cBus, cfg.Sda, cfg.Scl);

                        chip = CreateChip(cfg);
                        chip.Open(cfg.I2cBus, cfg.Address);

                        _chip = chip;
                        _cfg = cfg;
                        _state.Count = 0;
                        _primaryId = -1;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (chip != null)
                                chip.Dispose();
                        }
                        catch (Exception) { }

                        return EvalResult.Failure(engine, TouchInitError, name, "failed to initialize touch controller", ex.Message);
                    }
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveTouchClose(MogwaiNanoEngine engine, string name)
        {
            Cleanup();
            return EvalResult.NoError;
        }

        private static TouchChip CreateChip(TouchConfig cfg)
        {
            if (cfg.Controller == null)
                return new RegisterTouchChip(cfg.Custom);

            string c = cfg.Controller.ToLower();

            if (c == "cst816")
                return new RegisterTouchChip(TouchProfiles.Cst816());

            if (c == "ft6x36")
                return new RegisterTouchChip(TouchProfiles.Ft6x36());

            if (c == "gt911")
                return new Gt911Chip();

            throw new ArgumentException("unknown controller");
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
        }

        private static EvalResult ParseConfig(MogwaiNanoEngine engine, MOGRecord rec, string name, out TouchConfig cfg)
        {
            cfg = new TouchConfig();

            var rd = new RecordReader(rec);

            cfg.Controller = rd.Str("controller");
            cfg.I2cBus = rd.Int("i2cBus", false, 1);
            cfg.Sda = rd.Int("sda", true, -1);
            cfg.Scl = rd.Int("scl", true, -1);
            cfg.Address = rd.Int("address", false, -1);
            cfg.Width = rd.Int("width", true, 0);
            cfg.Height = rd.Int("height", true, 0);
            cfg.PollMs = rd.Int("pollMs", false, 20);
            cfg.SwapXY = rd.Bool("swapXY", false);
            cfg.FlipX = rd.Bool("flipX", false);
            cfg.FlipY = rd.Bool("flipY", false);
            cfg.Debug = rd.Bool("debug", false);

            if (cfg.Controller == null)
            {
                // Custom controller: the read layout comes from the record.
                var p = new TouchProfile();

                p.ReadReg = rd.Int("readReg", true, 0);
                p.ReadLen = rd.Int("readLen", true, 0);
                p.CountIndex = rd.Int("countIndex", true, 0);
                p.CountMask = rd.Int("countMask", false, 0x0F);
                p.PointBase = rd.Int("pointBase", false, 0);
                p.PointStride = rd.Int("pointStride", false, 0);
                p.MaxPoints = rd.Int("maxPoints", false, 1);
                p.XHi = rd.Int("xHi", true, 0);
                p.XLo = rd.Int("xLo", true, 0);
                p.YHi = rd.Int("yHi", true, 0);
                p.YLo = rd.Int("yLo", true, 0);
                p.HiMask = rd.Int("hiMask", false, 0x0F);
                p.IdByte = rd.Int("idByte", false, -1);
                p.IdShift = rd.Int("idShift", false, 0);
                p.IdMask = rd.Int("idMask", false, 0x0F);

                if (rd.Problem == null && cfg.Address < 0)
                    rd.Problem = "address: is mandatory when controller: is not given";

                cfg.Custom = p;
            }

            if (rd.Problem != null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, rd.Problem);

            if (cfg.Width <= 0 || cfg.Height <= 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "width: and height: must be greater than 0");

            if (cfg.PollMs < 5)
                cfg.PollMs = 5;

            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Coordinates
        // ------------------------------------------------------------------

        // Converts native touch coordinates to screen coordinates (flips, swap, screen rotation).
        private static void Map(int tx, int ty, out int x, out int y)
        {
            int w = _cfg.Width;
            int h = _cfg.Height;
            int t;

            if (_cfg.SwapXY)
            {
                t = tx; tx = ty; ty = t;
                t = w; w = h; h = t;
            }
            if (_cfg.FlipX) tx = w - 1 - tx;
            if (_cfg.FlipY) ty = h - 1 - ty;

            w--;
            h--;

            int rotation = GetRotation != null ? GetRotation() : 0;

            switch (rotation & 3)
            {
                case 1: x = ty; y = w - tx; break;
                case 2: x = w - tx; y = h - ty; break;
                case 3: x = h - ty; y = tx; break;
                default: x = tx; y = ty; break;
            }
        }

        // ------------------------------------------------------------------
        // Primary finger tracking
        // ------------------------------------------------------------------

        // Index of the primary finger in the current state, -1 if it is not down.
        private static int FindPrimary()
        {
            if (_primaryId < 0)
                return -1;

            for (int i = 0; i < _state.Count; i++)
            {
                if (_state.Id[i] == _primaryId)
                    return i;
            }

            return -1;
        }

        // ------------------------------------------------------------------
        // Polling thread and events
        // ------------------------------------------------------------------

        private static void PollLoop()
        {
            while (true)
            {
                int delay;
                MogwaiNanoEngine engine;
                string eventName = null;
                int px = 0;
                int py = 0;
                int count = 0;

                lock (_lock)
                {
                    if (!_running || _chip == null)
                        return;

                    delay = _cfg.PollMs;
                    engine = _eventEngine;

                    try
                    {
                        // When the chip has no new data the previous state is kept.
                        _chip.Read(_state);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("touch read error: " + ex.Message);
                    }

                    count = _state.Count;
                    int index = FindPrimary();

                    if (_primaryId >= 0 && index < 0)
                    {
                        // The primary finger was lifted. If other fingers are still down, one of them
                        // becomes the primary finger at the next iteration, with a new press event.
                        eventName = EVENT_TOUCH_DID_RELEASE;
                        px = _lastX;
                        py = _lastY;
                        _primaryId = -1;
                    }
                    else if (_primaryId < 0 && count > 0)
                    {
                        _primaryId = _state.Id[0];
                        Map(_state.X[0], _state.Y[0], out px, out py);
                        _lastX = px;
                        _lastY = py;
                        eventName = EVENT_TOUCH_DID_PRESS;
                    }
                    else if (_primaryId >= 0)
                    {
                        Map(_state.X[index], _state.Y[index], out px, out py);

                        if (px != _lastX || py != _lastY)
                        {
                            _lastX = px;
                            _lastY = py;
                            eventName = EVENT_TOUCH_DID_MOVE;
                        }
                    }

                    if (eventName != null && _cfg.Debug)
                        Debug.WriteLine(eventName + " x=" + px + " y=" + py + " n=" + count);
                }

                if (eventName != null && engine != null)
                    FireTouchEvent(engine, eventName, px, py, count);

                Thread.Sleep(delay);
            }
        }

        // count = number of fingers still down once the event has happened.
        private static void FireTouchEvent(MogwaiNanoEngine engine, string eventName, int x, int y, int count)
        {
            try
            {
                var data = new MOGRecord(engine);
                data.SetItem("x", new MOGNumber(engine, x));
                data.SetItem("y", new MOGNumber(engine, y));
                data.SetItem("count", new MOGNumber(engine, count));

                engine.FireEvent(eventName, data);
            }
            catch (Exception)
            {
            }
        }

        public static EvalResult PrimitiveTouchStart(MogwaiNanoEngine engine, string name)
        {
            // touch.start   (events are sent to the engine that calls it)

            lock (_lock)
            {
                if (_chip == null)
                    return EvalResult.Failure(engine, TouchIsClosedError, name);

                if (_running)
                    return EvalResult.NoError;

                _primaryId = -1;
                _eventEngine = engine;
                _running = true;
                _thread = new Thread(PollLoop);
                _thread.Start();
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTouchStop(MogwaiNanoEngine engine, string name)
        {
            // touch.stop

            StopPolling();
            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Reading from a script
        // ------------------------------------------------------------------

        // When the polling thread is not running, the chip is read on demand.
        private static void RefreshIfIdle()
        {
            if (!_running)
                _chip.Read(_state);
        }

        public static EvalResult PrimitiveTouchCount(MogwaiNanoEngine engine, string name)
        {
            // touch.count -> number of fingers

            lock (_lock)
            {
                if (_chip == null)
                    return EvalResult.Failure(engine, TouchIsClosedError, name);

                try
                {
                    RefreshIfIdle();
                    engine.StackPush(new MOGNumber(engine, _state.Count));
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, TouchOperationError, name, ex.Message);
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PushPoint(MogwaiNanoEngine engine, string name, int index)
        {
            lock (_lock)
            {
                if (_chip == null)
                    return EvalResult.Failure(engine, TouchIsClosedError, name);

                try
                {
                    RefreshIfIdle();

                    if (index == PrimaryFinger)
                    {
                        // The followed finger when events are running, otherwise the first one.
                        index = FindPrimary();

                        if (index < 0)
                            index = 0;
                    }

                    int x = -1;
                    int y = -1;

                    if (index >= 0 && index < _state.Count)
                        Map(_state.X[index], _state.Y[index], out x, out y);

                    engine.StackPush(new MOGNumber(engine, x));
                    engine.StackPush(new MOGNumber(engine, y));
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, TouchOperationError, name, ex.Message);
                }
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveTouchRead(MogwaiNanoEngine engine, string name)
        {
            // touch.read -> x y   (primary finger, in screen coordinates; -1 -1 if nobody touches)
            return PushPoint(engine, name, PrimaryFinger);
        }

        public static EvalResult PrimitiveTouchReadPoint(MogwaiNanoEngine engine, string name)
        {
            // n touch.readPoint -> x y   (finger n in the chip's list, -1 -1 if absent)

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var index = engine.StackPop() as MOGNumber;
                return PushPoint(engine, name, (int)index.Value);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}