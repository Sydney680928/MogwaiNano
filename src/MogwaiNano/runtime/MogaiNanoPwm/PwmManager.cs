using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.Pwm;

namespace MogaiNanoPwm
{
    public class PwmManager
    {
        private static PwmManager _instance;
        private static Hashtable _openedPwm = new();

        public static PwmManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PwmManager();

                return _instance;
            }
        }

        public readonly static Error PwmAlreadyOpenedError = new Error("PWM.1", "pwm already opened error");
        public readonly static Error PwmOpenError = new Error("PWM.2", "pwm open error");
        public readonly static Error PwmUnknownNameError = new Error("PWM.3", "pwm unknown name error");
        public readonly static Error PwmStartError = new Error("PWM.4", "pwm start error");
        public readonly static Error PwmStopError = new Error("PWM.5", "pwm stop error");

        private static PwmChannel GetPwmChannel(int engineId, string chanelName)
        {
            if (_openedPwm.Contains(engineId))
            {
                var pwmChanels = _openedPwm[engineId] as Hashtable;

                if (pwmChanels.Contains(chanelName))
                    return pwmChanels[chanelName] as PwmChannel;
            }
                      
            return null;
        }

        private static bool RemovePwmChannel(int engineId, string chanelName)
        {
            if (_openedPwm.Contains(engineId))
            {
                var pwmChanels = _openedPwm[engineId] as Hashtable;

                if (pwmChanels.Contains(chanelName))
                {
                    pwmChanels.Remove(chanelName);
                    return true;
                }
            }

            return false;
        }

        private static void AddPwmChannel(int engineId, string chanelName, PwmChannel pwmChannel)
        {
            if (!_openedPwm.Contains(engineId))
                _openedPwm[engineId] = new Hashtable();
            
            var pwmChanels = _openedPwm[engineId] as Hashtable;
            pwmChanels[chanelName] = pwmChannel;
        }

        public static void Cleanup(MogwaiNanoEngine engine)
        {
            if (_openedPwm.Contains(engine.EngineId))
            {
                var channels = _openedPwm[engine.EngineId] as Hashtable;

                foreach (string key in channels.Keys)
                {
                    var channel = channels[key] as PwmChannel;

                    try
                    {
                        channel.Stop();
                        channel.Dispose();
                    }
                    catch
                    {

                    }
                }

                channels.Clear();

                _openedPwm.Remove(engine.EngineId);
            }
        }

        public static EvalResult PrimitivePwmOpen(MogwaiNanoEngine engine, string name)
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
   
                if (GetPwmChannel(engine.EngineId, pwmName.Value) != null)
                    return EvalResult.Failure(engine, PwmAlreadyOpenedError, name);

                int nPin = (int)pin.Value;

                try
                {
                    var pwmChannel = PwmChannel.CreateFromPin(nPin, (int)frequency.Value, dutyCycle.Value / 100.0);

                    if (pwmChannel == null)
                        return EvalResult.Failure(engine, PwmOpenError, name, $"failed to open PWM on pin {nPin}");

                    AddPwmChannel(engine.EngineId, pwmName.Value, pwmChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, PwmOpenError, name, $"failed to open PWM on pin {nPin}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitivePwmClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' pwm.close

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetPwmChannel(engine.EngineId, pwmName.Value); 

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, PwmUnknownNameError, name);

                try
                {
                    pwmChannel.Stop();
                    pwmChannel.Dispose();
                }
                catch
                {

                }   

                RemovePwmChannel(engine.EngineId, pwmName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitivePwmStart(MogwaiNanoEngine engine, string name)
        {
            // name pwm.start

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetPwmChannel(engine.EngineId, pwmName.Value);

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, PwmUnknownNameError, name);

                try
                {
                    pwmChannel.Start();
                    return EvalResult.NoError;
                }
                catch
                {
                    return EvalResult.Failure(engine, PwmStartError, name);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitivePwmStop(MogwaiNanoEngine engine, string name)
        {
            // name pwm.stop

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;
                var pwmChannel = GetPwmChannel(engine.EngineId, pwmName.Value);

                if (pwmChannel == null)
                    return EvalResult.Failure(engine, PwmUnknownNameError, name);

                try
                {
                    pwmChannel.Stop();
                    return EvalResult.NoError;
                }
                catch
                {
                    return EvalResult.Failure(engine, PwmStopError, name);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}
