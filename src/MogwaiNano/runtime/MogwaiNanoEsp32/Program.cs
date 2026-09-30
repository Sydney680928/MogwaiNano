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
