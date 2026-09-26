using MogwaiNano.Engine;
using MogwaiNano.Interfaces;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.Pwm;
using System.Diagnostics;
using static MogwaiNano.Engine.MogwaiNanoEngine;

namespace MogwaiNanoPWM
{
    public class MogwaiNanoPWM : IPlugin
    {
        private static Hashtable _pwmChannels;

        private static Error _pwmAlreadyOpenedError;
        private static Error _pwmOpenError;
        private static Error _pwmUnknownNameError;

        public string Name => "PWM";

        public string Description => "MogwaiNano plugin that provides PWM functionality.";

        public System.Collections.Hashtable Primitives { get; } = new();

        public ArrayList Errors { get; } = new();

        public void CleanUp(int engineId)
        {
            // Cleanup PWM channels when the engine is reset or disposed

            Debug.WriteLine($"CleanUp plugin {Name} for engine {engineId}");

            if (_pwmChannels.Contains(engineId))
            {
                var pwmChannels = _pwmChannels[engineId] as Hashtable;

                if (pwmChannels != null)
                {
                    foreach (var key in pwmChannels.Keys)
                    {
                        if (pwmChannels[key] is PwmChannel pwmChannel)
                        {
                            pwmChannel.Dispose();
                            Debug.WriteLine($"CleanUp PWM channel '{key}'");
                        }
                    }

                    _pwmChannels.Remove(engineId);
                }
            }
        }

        public void Initialize(MogwaiNanoEngine engine)
        {
            _pwmChannels = new();

            Primitives.Add("pwm2.open", new PrimitiveDelegate(PrimitivePwmOpen));
            Primitives.Add("pwm2.close", new PrimitiveDelegate(PrimitivePwmClose));
            Primitives.Add("pwm2.start", new PrimitiveDelegate(PrimitivePwmStart));
            Primitives.Add("pwm2.stop", new PrimitiveDelegate(PrimitivePwmStop));

            _pwmAlreadyOpenedError = new Error("PWM.1", "pwm already opened error");
            _pwmOpenError = new Error("PWM.2", "pwm open error");
            _pwmUnknownNameError = new Error("PWM.3", "pwm unknown name error");

            Errors.Add(_pwmAlreadyOpenedError);
            Errors.Add(_pwmOpenError);
            Errors.Add(_pwmUnknownNameError);
        }

        private static PwmChannel GetChannel(MogwaiNanoEngine engine, string name)
        {
            if (_pwmChannels.Contains(engine.EngineId))
            {
                var dic = _pwmChannels[engine.EngineId] as Hashtable;

                if (dic.Contains(name))
                    return dic[name] as PwmChannel;
            }

            return null;
        }

        private static void AddChannel(MogwaiNanoEngine engine, string name, PwmChannel channel)
        {
            Hashtable dic = null;

            if (!_pwmChannels.Contains(engine.EngineId))
            {
                dic = new Hashtable();
                _pwmChannels.Add(engine.EngineId, dic);
            }
            else
            {
                dic = _pwmChannels[engine.EngineId] as Hashtable;
            }

            dic[name] = channel;
            Debug.WriteLine($"'{name}' added to PWM channels for engine {engine.EngineId}");
        }

        private static void RemoveChannel(MogwaiNanoEngine engine, string name)
        {
            if (_pwmChannels.Contains(engine.EngineId))
            {
                var dic = _pwmChannels[engine.EngineId] as Hashtable;

                if (dic.Contains(name))
                {
                    dic.Remove(name);
                    Debug.WriteLine($"'{name}' removed from PWM channels for engine {engine.EngineId}");
                }
            }
        }


        private static EvalResult PrimitivePwmOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' pin frequency dutyCycle pwm.open

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGName))
            {
                var dutyCycle = engine.StackPop() as MOGNumber;
                var frequency = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;
                var pwmName = engine.StackPop() as MOGName;

                if (dutyCycle.Value < 0 || dutyCycle.Value > 100)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "PWM duty cycle must be between 0 and 100");

                if (frequency.Value <= 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "PWM frequency must be greater than 0");

                var pwmChannel = GetChannel(engine, pwmName.Value);

                if (pwmChannel != null)
                    return EvalResult.Failure(engine, _pwmAlreadyOpenedError, name);

                int nPin = (int)pin.Value;

                try
                {
                    pwmChannel = PwmChannel.CreateFromPin(nPin, (int)frequency.Value, dutyCycle.Value / 100.0);

                    if (pwmChannel == null)
                        return EvalResult.Failure(engine, _pwmOpenError, name, $"failed to open PWM on pin {nPin}");

                    AddChannel(engine,  pwmName.Value, pwmChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, _pwmOpenError, name, $"failed to open PWM on pin {nPin}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' pwm.close

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetChannel(engine, pwmName.Value); 

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, _pwmUnknownNameError, name);

                pwmChannel.Stop();
                pwmChannel.Dispose();

                RemoveChannel(engine, pwmName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmStart(MogwaiNanoEngine engine, string name)
        {
            // name pwm.start

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetChannel(engine, pwmName.Value);

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, _pwmUnknownNameError, name);

                pwmChannel.Start();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmStop(MogwaiNanoEngine engine, string name)
        {
            // name pwm.stop

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetChannel(engine, pwmName.Value); 

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, _pwmUnknownNameError, name);

                pwmChannel.Stop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}
