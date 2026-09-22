using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Device.Adc;
using static MogwaiNano.Engine.MogwaiNanoEngine;

namespace MogwaiNanoADC
{
    public class MogwaiNanoADC : MogwaiNano.Interfaces.IPlugin
    {
        public string Name => "MogwaiNano ADC";

        public string Description => "A plugin for MogwaiNano that provides ADC functionality.";

        public System.Collections.Hashtable Primitives { get; } = new();

        public MogwaiNanoADC()
        {
            Primitives.Add("adc2.open", new PrimitiveDelegate(PrimitiveAdcOpen));
            Primitives.Add("adc2.close", new PrimitiveDelegate(PrimitiveAdcClose));
            Primitives.Add("adc2.read", new PrimitiveDelegate(PrimitiveAdcReadValue));
            Primitives.Add("adc2.resolutionInBits", new PrimitiveDelegate(PrimitiveAdcGetResolutionInBits));
            Primitives.Add("adc2.maxValue", new PrimitiveDelegate(PrimitiveAdcGetMaxValue));
        }

        private static EvalResult PrimitiveAdcOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' channel adc.open

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGName))
            {
                var channel = engine.StackPop() as MOGNumber;
                var adcName = engine.StackPop() as MOGName;

                if (engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcAlreadyOpenedError, name);

                int nChannel = (int)channel.Value;

                try
                {
                    var adcChannel = engine.AdcController.OpenChannel(nChannel);
                    engine.AdcChannels.Add(adcName.Value, adcChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.AdcOpenError, name, $"failed to open ADC channel {nChannel}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' adc.close

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;

                if (!engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcUnknownNameError, name);

                var adcChannel = engine.AdcChannels[adcName.Value] as AdcChannel;
                adcChannel.Dispose();

                engine.AdcChannels.Remove(adcName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcReadValue(MogwaiNanoEngine engine, string name)
        {
            // name adc.readValue

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;

                if (!engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcUnknownNameError, name);

                var adcChannel = engine.AdcChannels[adcName.Value] as AdcChannel;
                var value = adcChannel.ReadValue();

                engine.StackPush(new MOGNumber(engine, value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcGetMaxValue(MogwaiNanoEngine engine, string name)
        {
            // adc.maxValue

            var maxValue = engine.AdcController.MaxValue;
            engine.StackPush(new MOGNumber(engine, maxValue));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveAdcGetResolutionInBits(MogwaiNanoEngine engine, string name)
        {
            // adc.resolutionInBits

            var resolutionInBits = engine.AdcController.ResolutionInBits;
            engine.StackPush(new MOGNumber(engine, resolutionInBits));

            return EvalResult.NoError;
        }
    }
}
