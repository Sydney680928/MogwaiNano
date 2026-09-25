using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.Adc;
using System.Diagnostics;



namespace MogwaiNanoADC
{
    public class MogwaiNanoADC : MogwaiNano.Interfaces.IPlugin
    {
        private static AdcController _adcController;
        private static Hashtable _adcChannels;

        private static Error _adcAlreadyOpenedError;
        private static Error _adcOpenError;
        private static Error _adcUnknownNameError;

        public string Name => "ADC";

        public string Description => "MogwaiNano plugin that provides ADC functionality.";

        public Hashtable Primitives { get; } = new();

        public ArrayList Errors { get; } = new();

        public MogwaiNanoADC()
        {
            
        }

        public void Initialize(MogwaiNanoEngine engine)
        {
            _adcController = new();
            _adcChannels = new();

            Primitives.Add("adc2.open", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveAdcOpen));
            Primitives.Add("adc2.close", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveAdcClose));
            Primitives.Add("adc2.read", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveAdcReadValue));
            Primitives.Add("adc2.resolutionInBits", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveAdcGetResolutionInBits));
            Primitives.Add("adc2.maxValue", new MogwaiNanoEngine.PrimitiveDelegate(PrimitiveAdcGetMaxValue));

            _adcAlreadyOpenedError = new Error("ADC.1", "adc already opened error");
            _adcOpenError = new Error("ADC.2", "adc open error");
            _adcUnknownNameError = new Error("ADC.3", "adc unknown name error");

            Errors.Add(_adcAlreadyOpenedError);
            Errors.Add(_adcOpenError);
            Errors.Add(_adcUnknownNameError);
        }

        public void CleanUp(int engineId)
        {
            // Cleanup ADC channels when the engine is reset or disposed

            Debug.WriteLine($"CleanUp plugin {Name} for engine {engineId}");

            if (_adcChannels.Contains(engineId))
            {
                var adcChannels = _adcChannels[engineId] as Hashtable;

                if (adcChannels != null)
                {
                    foreach (var key in adcChannels.Keys)
                    {
                        if (adcChannels[key] is AdcChannel adcChannel)
                        {
                            adcChannel.Dispose();
                            Debug.WriteLine($"CleanUp ADC channel '{key}'");
                        }
                    }

                    _adcChannels.Remove(engineId);
                }
            }
        }

        private static AdcChannel GetChannel(MogwaiNanoEngine engine, string name)
        {
            if (_adcChannels.Contains(engine.EngineId))
            {
                var dic = _adcChannels[engine.EngineId] as Hashtable;
               
                if (dic.Contains(name))
                    return dic[name] as AdcChannel;
            }

            return null;
        }

        private static void AddChannel(MogwaiNanoEngine engine, string name, AdcChannel channel)
        {
            Hashtable dic = null;

            if (!_adcChannels.Contains(engine.EngineId))
            {
                dic = new Hashtable();
                _adcChannels.Add(engine.EngineId, dic);
            }
            else
            {
                dic = _adcChannels[engine.EngineId] as Hashtable;
            }

            dic[name] = channel;
            Debug.WriteLine($"'{name}' added to ADC channels for engine {engine.EngineId}");
        }

        private static void RemoveChannel(MogwaiNanoEngine engine, string name)
        {         
            if (_adcChannels.Contains(engine.EngineId))
            {
                var dic = _adcChannels[engine.EngineId] as Hashtable;

                if (dic.Contains(name))
                {
                    dic.Remove(name);
                    Debug.WriteLine($"'{name}' removed from ADC channels for engine {engine.EngineId}");
                }
            }
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

                var adcChannel = GetChannel(engine, adcName.Value);

                if (adcChannel != null)
                    return EvalResult.Failure(engine, _adcAlreadyOpenedError, name);

                int nChannel = (int)channel.Value;

                try
                {
                    adcChannel = _adcController.OpenChannel(nChannel);
                    AddChannel(engine, adcName.Value, adcChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, _adcOpenError, name, $"failed to open ADC channel {nChannel}", ex.Message);
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

                var adcChannel = GetChannel(engine, adcName.Value);
                    
                if (adcChannel == null)
                    return EvalResult.Failure(engine, _adcUnknownNameError, name);
                
                adcChannel.Dispose();

                RemoveChannel(engine, adcName.Value);   

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
                var adcChannel = GetChannel(engine, adcName.Value); 

                if (adcChannel == null)
                    return EvalResult.Failure(engine, _adcUnknownNameError, name);
                
                var value = adcChannel.ReadValue();
                engine.StackPush(new MOGNumber(engine, value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcGetMaxValue(MogwaiNanoEngine engine, string name)
        {
            // adc.maxValue

            var maxValue = _adcController.MaxValue;
            engine.StackPush(new MOGNumber(engine, maxValue));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveAdcGetResolutionInBits(MogwaiNanoEngine engine, string name)
        {
            // adc.resolutionInBits

            var resolutionInBits = _adcController.ResolutionInBits;
            engine.StackPush(new MOGNumber(engine, resolutionInBits));

            return EvalResult.NoError;
        }
    }
}
