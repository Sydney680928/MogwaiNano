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
    public class MOGEvent : MOGFireObject
    {
        // Maximum number of occurrences of the same event waiting in the queue

        public const int MaxPending = 8;

        private int _pending;
        private int _dropped;

        // The registered event this occurrence was cloned from (null on the registered event itself)

        private MOGEvent _origin;

        public int Pending => _pending;

        public int Dropped => _dropped;

        public MOGEvent(MogwaiNanoEngine engine, string name, MOGFunction function) : base(engine, name, function)
        {

        }

        // Called on the registered event, before a new occurrence is queued.
        // Returns false if the queue already holds MaxPending occurrences of this event.

        public bool TryReserve()
        {
            if (Interlocked.Increment(ref _pending) > MaxPending)
            {
                Interlocked.Decrement(ref _pending);
                Interlocked.Increment(ref _dropped);
                return false;
            }

            return true;
        }

        public void ResetPending()
        {
            Interlocked.Exchange(ref _pending, 0);
        }

        public override void OnDequeued()
        {
            var registered = _origin;

            if (registered != null && Interlocked.Decrement(ref registered._pending) < 0)
                Interlocked.Exchange(ref registered._pending, 0);
        }

        public override MOGObject Clone()
        {
            var func = Function.Clone() as MOGFunction;
            var obj = new MOGEvent(Engine, Name, func);
            obj._origin = _origin ?? this;
            obj.DebugInfo = DebugInfo;
            return obj;
        }
    }
}
