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
using System.Collections;
using System.Device.Gpio;

namespace MogwaiNanoGpio
{
    public class GpioManager
    {
        public static Hashtable OpenedPins { get; } = new(3);

        public static ArrayList Engines { get; } = new();

        public static GpioController GpioController { get; } = new();

        public static GpioPin GetPin(int pinNumber)
        {
            if (OpenedPins.Contains(pinNumber))
                return OpenedPins[pinNumber] as GpioPin;

            return null;
        }

        public static bool ClosePin(int pinNumber)
        {
            if (OpenedPins.Contains(pinNumber))
            {
                var pin = OpenedPins[pinNumber] as GpioPin;
                OpenedPins.Remove(pinNumber);
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
                return true;
            }

            return false;
        }

        private static EvalResult GpioPinWrite(MogwaiNanoEngine engine, string name, PinValue pinValue)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"unknown pin {numPin.Value}");

            pin.Write(pinValue);

            return EvalResult.NoError;
        }

        public static void Cleanup()
        {
            foreach (int pinNumber in OpenedPins.Keys)
            {
                var pin = OpenedPins[pinNumber] as GpioPin;
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
                pin = null;
            }

            OpenedPins.Clear();
            Engines.Clear();
        }

        private static EvalResult SetPinMode(MogwaiNanoEngine engine, string name, PinMode mode)
        {
            // Used by all pin mode known
            // 4 gpio.setMode.xxx

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (OpenedPins.Contains(nPin))
            {
                var pin = OpenedPins[nPin] as GpioPin;
                pin.SetPinMode(mode);
            }
            else
            {
                GpioPin newPin = GpioController.OpenPin(nPin, mode);
                OpenedPins.Add(nPin, newPin);

                newPin.ValueChanged += GpioPin_ValueChanged;
            }

            if (!Engines.Contains(engine))
                Engines.Add(engine);

            return EvalResult.NoError;
        }

        public static void GpioPin_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            var eventType = e.ChangeType == PinEventTypes.Rising ? 1 : 0;

            foreach (MogwaiNanoEngine engine in Engines)
            {
                var record = new MOGRecord(engine);
                record.SetItem("pin", new MOGNumber(engine, e.PinNumber));
                record.SetItem("eventType", new MOGNumber(engine, eventType));
                
                engine.FireEvent("GPIO_PIN_CHANGED", record);
            }
        }

        public static EvalResult PrimitiveGpioModeInput(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.Input);

        public static EvalResult PrimitiveGpioSetModeInputPullDown(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.InputPullDown);

        public static EvalResult PrimitiveGpioSetModeInputPullUp(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.InputPullUp);    
        
        public static EvalResult PrimitiveGpioSetModeOutput(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.Output);

        public static EvalResult PrimitiveGpioPinWriteHigh(MogwaiNanoEngine engine, string name) => GpioPinWrite(engine, name, PinValue.High);

        public static EvalResult PrimitiveGpioPinWriteLow(MogwaiNanoEngine engine, string name) => GpioPinWrite(engine, name, PinValue.Low);
        
        public static EvalResult PrimitiveGpioPinRead(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"unknown pin {numPin.Value}");

            var e = pin.Read();
            engine.StackPush(new MOGNumber(engine, e == PinValue.High ? 1 : 0));

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveGpioPinToggle(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"unknown pin {numPin.Value}");

            pin.Toggle();

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveGpioPinClose(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (!ClosePin(nPin))
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"unknown pin {numPin.Value}");

            return EvalResult.NoError;
        }
    }
}
