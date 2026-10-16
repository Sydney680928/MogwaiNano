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

using Iot.Device.Ssd13xx;
using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.I2c;
using static Iot.Device.Ssd13xx.Ssd13xx;

namespace MogwaiNanoSsd1306
{
    public class SSD1306Manager
    {
        private static Hashtable _engines = new();

        public static Error SSD1306InitError = new("SSD1306.1", "ssd1306 init error");
        public static Error SSD1306IsClosedError = new("SSD1306.2", "ssd1306 already closed error");
        public static Error SSD1306IsOpenedError = new("SSD1306.3", "ssd1306 already opened error");
        public static Error SSD1306OperationError = new("SSD1306.4", "ssd1306 operation error");

        public static void Cleanup(MogwaiNanoEngine engine)
        {
            if (_engines.Contains(engine.EngineId))
            {
                var screen = _engines[engine.EngineId] as Ssd1306;
                screen.Dispose();
                screen = null;
                
                _engines.Remove(engine.EngineId);
            }
        }

        public static EvalResult PrimitiveSsd1306Init(MogwaiNanoEngine engine, string name)
        {
            // bus address ssd1306.init

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var address = engine.StackPop() as MOGNumber;
                var bus = engine.StackPop() as MOGNumber;
          
                if (_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsOpenedError, name);

                int busNumber = (int)bus.Value;
                int addressNumber = (int)address.Value;

                if (busNumber < 1 || busNumber > 2)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C bus number must be between 1 and 2");

                if (addressNumber < 0 || addressNumber > 127)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C address number must be between 0 and 127");

                Ssd1306 screen = null;

                try
                {
                    I2cConnectionSettings settings = new(busNumber, addressNumber, I2cBusSpeed.FastMode);
                    I2cDevice i2cDevice = I2cDevice.Create(settings);

                    screen = new(i2cDevice, DisplayResolution.OLED128x64);
                    screen.ClearScreen();

                    _engines[engine.EngineId] = screen; 
                }
                catch (Exception ex)
                {
                    if (screen != null)
                    {
                        screen.Dispose();
                        screen = null;
                    }

                    return EvalResult.Failure(engine, SSD1306InitError, name, "failed to initialize ssd1306 display", $"bus {busNumber}", $"address {addressNumber:X2}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306Close(MogwaiNanoEngine engine, string name)
        {
            if (_engines.Contains(engine.EngineId))
            {
                var screen = _engines[engine.EngineId] as Ssd1306;  
                screen.Dispose();
                screen = null;

                _engines.Remove(engine.EngineId);
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSsd1306Clear(MogwaiNanoEngine engine, string name)
        {
            if (!_engines.Contains(engine.EngineId))
                return EvalResult.Failure(engine, SSD1306IsClosedError, name);

            var screen = _engines[engine.EngineId] as Ssd1306;
            screen.ClearScreen();

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSsd1306PrintString(MogwaiNanoEngine engine, string name)
        {
            // x y text size center ssd1306.printString

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = engine.StackPop() as MOGBoolean;
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;  

                try
                {
                    if (screen.Font == null)
                        screen.Font = new BasicFont();

                    screen.Write((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawString(MogwaiNanoEngine engine, string name)
        {
            // x y text size center ssd1306.drawString

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = engine.StackPop() as MOGBoolean;
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    if (screen.Font == null)
                        screen.Font = new BasicFont();

                    screen.DrawString((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306Refresh(MogwaiNanoEngine engine, string name)
        {
            if (!_engines.Contains(engine.EngineId))
                return EvalResult.Failure(engine, SSD1306IsClosedError, name);

            var screen = _engines[engine.EngineId] as Ssd1306;
            screen.Display();

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSsd1306DrawPixel(MogwaiNanoEngine engine, string name)
        {
            // x y true ssd1306.drawPixel   

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    screen.DrawPixel((int)x.Value, (int)y.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawHorizontalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len true ssd1306.drawHorizontalLine  

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var len = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    screen.DrawHorizontalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawHVerticalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len true ssd1306.drawVerticalLine 

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var len = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    screen.DrawVerticalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h true ssd1306.drawRectangle

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    int vx = (int)x.Value;
                    int vy = (int)y.Value;
                    int vw = (int)width.Value;
                    int vh = (int)height.Value;

                    screen.DrawHorizontalLine(vx, vy, vw, set.Value);
                    screen.DrawVerticalLine(vx + vw - 1, vy, vh, set.Value);
                    screen.DrawHorizontalLine(vx, vy + vh - 1, vw, set.Value);
                    screen.DrawVerticalLine(vx, vy, vh, set.Value);

                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawFilledRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h true ssd1306.drawFilledRectangle

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    screen.DrawFilledRectangle((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSsd1306DrawBitmap(MogwaiNanoEngine engine, string name)
        {
            // x y w h data size ssd1306.drawBitmap

            var s = engine.StackSign(6);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber) && s[5] == typeof(MOGNumber))
            {
                var size = engine.StackPop() as MOGNumber;
                var data = engine.StackPop() as MOGData;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (!_engines.Contains(engine.EngineId))
                    return EvalResult.Failure(engine, SSD1306IsClosedError, name);

                var screen = _engines[engine.EngineId] as Ssd1306;

                try
                {
                    screen.DrawBitmap((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value, data.Items, (byte)size.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SSD1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}
