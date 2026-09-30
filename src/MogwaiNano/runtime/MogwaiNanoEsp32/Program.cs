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
using System.Threading;

namespace MogwaiNanoEsp32
{
    public class Program
    {
        public static void Main()
        {

            MogwaiNanoEngine.RegisterPrimitive("esp32.setPinFunction", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveDeviceSetPinFunction), true);

            MogwaiNanoCore.MogwaiNanoSystem.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static EvalResult PrimitiveDeviceSetPinFunction(MogwaiNanoEngine engine, string name)
        {
            // pin setvalue device.setPin

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var function = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;

                if (pin.Value < 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                try
                {
                    nanoFramework.Hardware.Esp32.Configuration.SetPinFunction((int)pin.Value, (nanoFramework.Hardware.Esp32.DeviceFunction)(int)function.Value);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.PlatformNotSupportedError, name, ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}
