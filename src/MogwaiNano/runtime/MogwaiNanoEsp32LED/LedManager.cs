using Iot.Device.Ws28xx.Esp32;
using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace MogwaiNanoEsp32LED
{
    /// <summary>
    /// Pilote la LED WS2812 intégrée (ESP32) : témoin d'état du runtime,
    /// avec possibilité pour un script MOGWAI de prendre le contrôle.
    /// </summary>
    public class LedManager
    {
        public static Error LedOperationError = new("LED.1", "led operation error");

        private const int TickMs = 100;
        private const int BlinkTicks = 3;
        private const int DefaultBrightness = 5;

        private static readonly object _lock = new object();

        private static Ws28xx _neo;
        private static BitmapImage _image;
        private static int _pin = -1;
        private static bool _enabled;
        private static bool _stop;
        private static Thread _thread;

        private static LedState _runtimeState = LedState.Off;
        private static bool _blinkOn = true;
        private static int _brightness = DefaultBrightness;

        private static bool _scriptOwned;
        private static object _ownerId;
        private static int _sr, _sg, _sb;

        /// <summary>True si la LED est initialisée et utilisable.</summary>
        public static bool IsEnabled
        {
            get { lock (_lock) { return _enabled; } }
        }

        // ------------------------------------------------------------------
        // API runtime
        // ------------------------------------------------------------------

        /// <summary>
        /// Initialise la LED sur la broche donnée. pin &lt; 0 : LED désactivée.
        /// Ne lève jamais d'exception : retourne false si la LED n'est pas utilisable.
        /// </summary>
        public static bool Initialize(int pin)
        {
            Shutdown();

            if (pin < 0)
                return false;

            lock (_lock)
            {
                try
                {
                    _pin = pin;
                    _neo = new Ws2812c(pin, 1);
                    _image = _neo.Image;
                    _enabled = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LED init failed: {ex.Message}");
                    ReleaseDriver();
                    _pin = -1;
                    _enabled = false;
                    return false;
                }

                _brightness = DefaultBrightness;
                _scriptOwned = false;
                _ownerId = null;
                _runtimeState = LedState.Off;
                _blinkOn = true;
                _stop = false;

                _thread = new Thread(Worker);
                _thread.Start();
            }

            return true;
        }

        /// <summary>Arrête le clignotement et éteint la LED.</summary>
        public static void Shutdown()
        {
            Thread thread;

            lock (_lock)
            {
                _stop = true;
                thread = _thread;
                _thread = null;
            }

            if (thread != null)
                thread.Join();

            lock (_lock)
            {
                if (_enabled)
                    Write(0, 0, 0);

                _enabled = false;
                ReleaseDriver();
                _pin = -1;
                _scriptOwned = false;
                _ownerId = null;
                _runtimeState = LedState.Off;
            }
        }

        /// <summary>
        /// Change l'état du runtime. Si un script a pris la LED, l'état est mémorisé
        /// mais pas affiché (sauf Paused / Stepping, toujours prioritaires).
        /// </summary>
        public static void SetState(LedState state)
        {
            lock (_lock)
            {
                if (!_enabled)
                    return;

                _runtimeState = state;
                _blinkOn = true;
                Render(1);
            }
        }

        /// <summary>
        /// À brancher sur ResetDidExecute : si ce moteur avait pris la LED,
        /// elle est rendue au runtime.
        /// </summary>
        public static void Cleanup(MogwaiNanoEngine engine)
        {
            lock (_lock)
            {
                if (_scriptOwned && _ownerId != null && _ownerId.Equals(engine.EngineId))
                    ReleaseInternal();
            }
        }

        // ------------------------------------------------------------------
        // Primitives MOGWAI
        // ------------------------------------------------------------------

        public static EvalResult PrimitiveLedSet(MogwaiNanoEngine engine, string name)
        {
            // r g b led.set

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber))
            {
                var b = engine.StackPop() as MOGNumber;
                var g = engine.StackPop() as MOGNumber;
                var r = engine.StackPop() as MOGNumber;

                if (!IsByte(r) || !IsByte(g) || !IsByte(b))
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "values must be between 0 and 255");

                return TakeControl(engine, name, (int)r.Value, (int)g.Value, (int)b.Value);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveLedOff(MogwaiNanoEngine engine, string name)
        {
            // led.off

            return TakeControl(engine, name, 0, 0, 0);
        }

        public static EvalResult PrimitiveLedBrightness(MogwaiNanoEngine engine, string name)
        {
            // 0-255 led.brightness

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var value = engine.StackPop() as MOGNumber;

            if (!IsByte(value))
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "brightness must be between 0 and 255");

            lock (_lock)
            {
                if (!_enabled)
                    return EvalResult.NoError;

                _brightness = (int)value.Value;

                if (!Render())
                    return EvalResult.Failure(engine, LedOperationError, name, "led driver failure");
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveLedRelease(MogwaiNanoEngine engine, string name)
        {
            // led.release

            lock (_lock)
            {
                if (_enabled && _scriptOwned)
                    ReleaseInternal();
            }

            return EvalResult.NoError;
        }

        // ------------------------------------------------------------------
        // Interne
        // ------------------------------------------------------------------

        private static bool IsByte(MOGNumber n)
        {
            return n.Value >= 0 && n.Value <= 255;
        }

        private static EvalResult TakeControl(MogwaiNanoEngine engine, string name, int r, int g, int b)
        {
            lock (_lock)
            {
                // Pas de LED sur cette carte (ou driver HS) : no-op silencieux.
                if (!_enabled)
                    return EvalResult.NoError;

                _scriptOwned = true;
                _ownerId = engine.EngineId;
                _sr = r;
                _sg = g;
                _sb = b;

                if (!Render())
                    return EvalResult.Failure(engine, LedOperationError, name, "led driver failure");
            }

            return EvalResult.NoError;
        }

        // À appeler sous verrou.
        private static void ReleaseInternal()
        {
            _scriptOwned = false;
            _ownerId = null;
            _blinkOn = true;
            Render();
        }

        // À appeler sous verrou.
        private static bool IsBlinking()
        {
            if (_scriptOwned)
                return false;

            return _runtimeState == LedState.Starting || _runtimeState == LedState.Parsing;
        }

        // À appeler sous verrou : calcule la couleur effective et l'envoie.

        private static bool Render() => Render(_brightness);

        private static bool Render(int brightness)
        {
            int r = 0, g = 0, b = 0;

            if (_runtimeState == LedState.Paused)
            {
                b = 255;
            }
            else if (_runtimeState == LedState.Stepping)
            {
                r = 255; g = 255; b = 255;
            }
            else if (_scriptOwned)
            {
                r = _sr; g = _sg; b = _sb;
            }
            else
            {
                switch (_runtimeState)
                {
                    case LedState.Starting:
                    case LedState.Parsing:
                        if (_blinkOn)
                        {
                            r = 255; g = 80;
                        }
                        break;

                    case LedState.Ready:
                    case LedState.Running:
                        g = 255;
                        break;

                    case LedState.Fault:
                    case LedState.ProgramError:
                        r = 255;
                        break;
                }
            }

            return Write(r, g, b);
        }

        // À appeler sous verrou. Libère le driver courant (sans exception).
        private static void ReleaseDriver()
        {
            try
            {
                var disposable = _neo as IDisposable;

                if (disposable != null)
                    disposable.Dispose();
            }
            catch (Exception)
            {
            }

            _neo = null;
            _image = null;
        }

        // À appeler sous verrou. Applique la luminosité globale.

        private static bool Write(int r, int g, int b) => Write(r, g, b, _brightness);

        private static bool Write(int r, int g, int b, int brightness)
        {
            if (!_enabled || _pin < 0)
                return false;

            try
            {
                // Le driver Ws28xx.Esp32 garde un pointeur natif sur le tampon de l'image :
                // après un compactage du GC, il pointe vers de la mémoire invalide (couleurs
                // aléatoires). On recrée donc le driver avant chaque envoi.
                ReleaseDriver();

                _neo = new Ws2812c(_pin, 1);
                _image = _neo.Image;

                _image.SetPixel(0, 0, Color.FromArgb(r * brightness / 255, g * brightness / 255, b * brightness / 255));
                _neo.Update();
                return true;
            }
            catch (Exception ex)
            {
                // Un driver en échec ne doit jamais faire tomber le runtime.
                Debug.WriteLine($"LED write failed: {ex.Message}");
                ReleaseDriver();
                _enabled = false;
                return false;
            }
        }

        private static void Worker()
        {
            int tick = 0;

            while (true)
            {
                Thread.Sleep(TickMs);

                lock (_lock)
                {
                    if (_stop)
                        return;

                    if (!_enabled)
                        continue;

                    if (_runtimeState == LedState.Stepping)
                    {
                        // Le flash blanc a duré un tick : retour au bleu.
                        _runtimeState = LedState.Paused;
                        Render();
                        continue;
                    }

                    if (IsBlinking())
                    {
                        tick++;

                        if (tick >= BlinkTicks)
                        {
                            tick = 0;
                            _blinkOn = !_blinkOn;
                            Render();
                        }
                    }
                    else
                    {
                        tick = 0;
                    }
                }
            }
        }
    }
}
