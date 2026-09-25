using MogwaiNano.Engine;
using MogwaiNano.Interfaces;
using MogwaiNano.Objects;
using System;
using System.Collections;
using static MogwaiNano.Engine.MogwaiNanoEngine;

namespace MogwaiNanoESP32
{
    public class Using : IPlugin
    {
        public string Name => "ESP32";

        public string Description => "MogwaiNano plugin that provides ESP32 device functionality.";

        public Hashtable Primitives { get; private set; }

        public ArrayList Errors { get; private set; }

        public Using()
        {

        }

        public void CleanUp(int engineId)
        {

        }

        public void Initialize(MogwaiNanoEngine engine)
        {
            Primitives = new();
            Errors = new();

            Primitives.Add("device.setPinFunction", new PrimitiveDelegate(PrimitiveDeviceSetPinFunction));
        }

        private static EvalResult PrimitiveDeviceSetPinFunction(MogwaiNanoEngine engine, string name)
        {
            // pin setvalue esp32.setPinFunction

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var setValue = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;

                if (pin.Value < 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                try
                {
                    nanoFramework.Hardware.Esp32.Configuration.SetPinFunction((int)pin.Value, (nanoFramework.Hardware.Esp32.DeviceFunction)(int)setValue.Value);
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
