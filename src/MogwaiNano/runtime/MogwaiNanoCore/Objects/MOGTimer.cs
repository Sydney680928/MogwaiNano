// Copyright 2015-2026 Stéphane Sibué
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
using System.Diagnostics;
using System.Threading;

namespace MogwaiNano.Objects
{
    public class MOGTimer : MOGFireObject
    {
        private Timer _timer;
        private int _pending;

        public int Interval { get; init; }

        public bool IsCyclic { get; init; }

        public bool Status => _timer != null;

        public bool IsLaterTimer { get; init; }

        public MOGTimer(MogwaiNanoEngine engine, string name, int interval, bool isCyclic, MOGFunction function, bool isLaterTimer) : base(engine, name, function)
        {
            Interval = interval;
            IsCyclic = isCyclic;
            IsLaterTimer = isLaterTimer;
        }

        public override void OnDequeued() => Interlocked.Exchange(ref _pending, 0);

        public EvalResult Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            return EvalResult.NoError;
        }

        public EvalResult Start()
        {
            Stop();

            _timer = new Timer(TimerCallback, null, Interval, Timeout.Infinite);
            return EvalResult.NoError;
        }

        private void TimerCallback(object state)
        {
            if (Interlocked.Exchange(ref _pending, 1) == 0)
                Engine.RegisterFireObject(this);
            
            if (IsLaterTimer)
            {
                // On supprime le timer qui est à usage unique

                Engine.PurgeTimer(Name);
            }
            else
            {
                if (IsCyclic)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }
    }
}
