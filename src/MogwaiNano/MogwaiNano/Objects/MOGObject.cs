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

using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public abstract class MOGObject
    {
        public MogwaiNanoEngine Engine { get; set; }

        public bool AutoEval { get; set; }

        public MOGType Type { get; set; }

        public MOGObject(MogwaiNanoEngine engine, MOGType type)
        {
            Engine = engine;
            Type = type;
        }

        public abstract MOGObject Clone();

        public virtual EvalResult EngineEval()
        {
            Engine.StackPush(this);
            return EvalResult.NoError;
        }

        public virtual EvalResult UserEval()
        {
            return EngineEval();
        }
    }
}
