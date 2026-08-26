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
    public class MOGPrimitive : MOGObject
    {
        public string Name { get; set; }

        public MOGPrimitive(MogwaiNanoEngine engine, string name) : base(engine, engine.TypePrimitive)
        {
            Name = name;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGPrimitive(Engine, Name);
            return obj;
        }

        public override EvalResult EngineEval() => Engine.ExecutePrimitive(Name);

        public override EvalResult UserEval() => EngineEval();

        public override string ToString()
        {
            return Name;
        }
    }
}
