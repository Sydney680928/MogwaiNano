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

using Iot.Device.Ws28xx.Esp32;
using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Diagnostics;
using System.Drawing;

namespace MogwaiNanoEsp32LED
{
    /// <summary>
    /// Pilote la LED WS2812 intégrée (ESP32) : témoin d'état du runtime en couleurs fixes,
    /// avec possibilité pour un script MOGWAI de prendre le contrôle.
    /// </summary>
    public class LedManager
    {
        public static Error LedOperationError = new("LED.1", "led operation error");

        private const int DefaultBrightness = 5;

        private static readonly object _lock = new object();

        private static Ws28xx _neo;
        private static BitmapImage _image;
        private static int _pin = -1;
        private static bool _enabled;

        private static LedState _runtimeState = LedState.Off;
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
            }

            return true;
        }

        /// <summary>Éteint la LED et libère le driver.</summary>
        public static void Shutdown()
        {
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
        /// mais pas affiché (sauf Paused, toujours prioritaire).
        /// La LED est systématiquement réécrite, même si l'état est inchangé.
        /// </summary>
        public static void SetState(LedState state)
        {
            lock (_lock)
            {
                if (!_enabled)
                    return;

                _runtimeState = state;
                Render();
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

        public static EvalResult PrimitiveLedOff(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 0, 0, 0);

        public static EvalResult PrimitiveLedGreen(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 0, 255, 0);

        public static EvalResult PrimitiveLedRed(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 255, 0, 0);

        public static EvalResult PrimitiveLedBlue(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 0, 0, 255);

        public static EvalResult PrimitiveLedOrange(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 255, 80, 0);

        public static EvalResult PrimitiveLedYellow(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 255, g: 160, 0);

        public static EvalResult PrimitiveLedPurple(MogwaiNanoEngine engine, string name) => TakeControl(engine, name, 160, 0, 255);

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
            Render();
        }

        // À appeler sous verrou : calcule la couleur effective et l'envoie.
        private static bool Render()
        {
            int r = 0, g = 0, b = 0;

            if (_runtimeState == LedState.Paused)
            {
                // Pause : orange, prioritaire sur la couleur du script.
                r = 255; g = 80;
            }
            else if (_scriptOwned)
            {
                r = _sr; g = _sg; b = _sb;
            }
            else
            {
                switch (_runtimeState)
                {
                    case LedState.Initializing:
                        r = 255; g = 80;
                        break;

                    case LedState.Idle:
                        g = 255;
                        break;

                    case LedState.Running:
                        b = 255;
                        break;

                    case LedState.Error:
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
        private static bool Write(int r, int g, int b)
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

                _image.SetPixel(0, 0, Color.FromArgb(r * _brightness / 255, g * _brightness / 255, b * _brightness / 255));
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
    }
}
