using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.Adc;

namespace MogwaiNanoAdc
{
    public class AdcManager
    {
        private static AdcController _adcController = new();
        private static Hashtable _openedAdcChannels = new();

        public static Error AdcAlreadyOpenedError = new Error("ADC.1", "adc already opened error");
        public static Error AdcOpenError = new Error("ADC.2", "adc open error");
        public static Error AdcUnknownNameError = new Error("ADC.3", "adc unknown name error");

        private static AdcChannel GetAdcChannel(int engineId, string chanelName)
        {
            if (_openedAdcChannels.Contains(engineId))
            {
                var adcChanels = _openedAdcChannels[engineId] as Hashtable;

                if (adcChanels.Contains(chanelName))
                    return adcChanels[chanelName] as AdcChannel;
            }

            return null;
        }

        private static bool RemoveAdcChannel(int engineId, string chanelName)
        {
            if (_openedAdcChannels.Contains(engineId))
            {
                var adcChanels = _openedAdcChannels[engineId] as Hashtable;

                if (adcChanels.Contains(chanelName))
                {
                    adcChanels.Remove(chanelName);
                    return true;
                }
            }

            return false;
        }

        private static void AddAdcChannel(int engineId, string chanelName, AdcChannel adcChannel)
        {
            if (!_openedAdcChannels.Contains(engineId))
                _openedAdcChannels[engineId] = new Hashtable();

            var adcChanels = _openedAdcChannels[engineId] as Hashtable;
            adcChanels[chanelName] = adcChannel;
        }

        public static void Cleanup(MogwaiNanoEngine engine)
        {
            if (_openedAdcChannels.Contains(engine.EngineId))
            {
                var channels = _openedAdcChannels[engine.EngineId] as Hashtable;

                foreach (string key in channels.Keys)
                {
                    var channel = channels[key] as AdcChannel;
                   
                    try
                    {
                        channel.Dispose();
                    }
                    catch
                    {

                    }
                }

                channels.Clear();

                _openedAdcChannels.Remove(engine.EngineId);
            }
        }

        public static EvalResult PrimitiveAdcOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' channel adc.open

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGName))
            {
                var channel = engine.StackPop() as MOGNumber;
                var adcName = engine.StackPop() as MOGName;

                if (GetAdcChannel(engine.EngineId, adcName.Value) != null)
                    return EvalResult.Failure(engine, AdcAlreadyOpenedError, name);

                int nChannel = (int)channel.Value;

                try
                {
                    var adcChannel = _adcController.OpenChannel(nChannel);
                    AddAdcChannel(engine.EngineId, adcName.Value, adcChannel);  
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, AdcOpenError, name, $"failed to open ADC channel {nChannel}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveAdcClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' adc.close

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;
                var adcChannel = GetAdcChannel(engine.EngineId, adcName.Value); 

                if (adcChannel == null)
                    return EvalResult.Failure(engine, AdcUnknownNameError, name);

                adcChannel.Dispose();

                RemoveAdcChannel(engine.EngineId, adcName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveAdcReadValue(MogwaiNanoEngine engine, string name)
        {
            // name adc.readValue

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;
                var adcChannel = GetAdcChannel(engine.EngineId, adcName.Value);
                
                if (adcChannel == null)                 
                    return EvalResult.Failure(engine, AdcUnknownNameError, name);

                var value = adcChannel.ReadValue();
                engine.StackPush(new MOGNumber(engine, value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveAdcGetMaxValue(MogwaiNanoEngine engine, string name)
        {
            // adc.maxValue

            var maxValue = _adcController.MaxValue;
            engine.StackPush(new MOGNumber(engine, maxValue));

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveAdcGetResolutionInBits(MogwaiNanoEngine engine, string name)
        {
            // adc.resolutionInBits

            var resolutionInBits = _adcController.ResolutionInBits;
            engine.StackPush(new MOGNumber(engine, resolutionInBits));

            return EvalResult.NoError;
        }

    }
}
