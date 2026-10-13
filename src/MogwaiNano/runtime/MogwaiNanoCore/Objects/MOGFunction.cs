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
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using GC = nanoFramework.Runtime.Native.GC;

namespace MogwaiNano.Objects
{
    public class MOGFunction : MOGCode
    {
        public string Name { get; internal set; } = Guid.NewGuid().ToString();

        public MOGFunction(MogwaiNanoEngine engine) : base(engine)
        {
            Type = engine.TypeFunction;
        }

        public MOGFunction(MogwaiNanoEngine engine, ArrayList items) : base(engine, items)
        {

        }


        public MOGFunction(MogwaiNanoEngine engine, string content, bool automaticMode = true, bool withProgressInformations = false) : base(engine, content, automaticMode, withProgressInformations)
        {

        }

        public override EvalResult Execute(bool debugCommand = false)
        {
            Engine.VarPushContext(Name);
            var r = base.Execute(debugCommand);
            //Engine.ReturnRequested = false;
            Engine.VarPopContext();

            return r;
        }

        public override EvalResult EngineEval()
        {
            if (AutoEval)
            {
                return Execute();
            }
            else
            {
                return base.EngineEval();
            }
        }

        public override MOGObject Clone()
        {
            var obj = new MOGFunction(Engine, Content, false);

            if (Engine.FrugalMode)
            {
                obj.AutoEval = Content.StartsWith("!");
            }
            else
            {
                if (Items != null)
                {
                    obj.Items = new();

                    foreach (MOGObject item in Items)
                        obj.Items.Add(item.Clone());
                }
            }

            if (DebugInfo != null)
                obj.DebugInfo = DebugInfo.Clone();

            return obj;
        }
        public override string ToString() => $"«{ToStringCode()}»";
    }
}
