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
using System.Collections;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGList : MOGObject
    {
        public ArrayList Items { get; } = new();

        public MOGList(MogwaiNanoEngine engine) : base(engine, engine.TypeList)
        {

        }   

        public MOGList(MogwaiNanoEngine engine, ArrayList items) : this(engine)
        {
            Items = items;
        }

        public MOGList(MogwaiNanoEngine engine, string content) : this(engine)
        {
            var parser = new Parser(engine);
            Items = parser.Parse(content);

            if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
            {
                AutoEval = true;
                Items.RemoveAt(0);
            }   
        }

        public MOGObject GetItem(int index)
        {
            if (index >= 0 && index < Items.Count)
                return Items[index] as MOGObject;

            return null;
        }

        public void AddItem(MOGObject item) => Items.Add(item);

        public bool SetItem(int index, MOGObject value)
        {
            if (index >= 0 && index < Items.Count)
            {
                Items[index] = value;
                return true;
            }

            return false;
        }   

        public override MOGObject Clone()
        {
            var obj = new MOGList(Engine);

            foreach (MOGObject item in Items)
                obj.Items.Add(item.Clone());

            obj.AutoEval = AutoEval;

            return obj;
        }

        private void Eval()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i] as MOGObject;
                var stackSize = Engine.StackSize;

                var r = item.EngineEval();

                if (r != EvalResult.NoError)
                    break;

                if (Engine.StackSize > stackSize)
                {
                    var value = Engine.StackPop();
                    Items[i] = value!;
                }
            }
        }

        public override EvalResult EngineEval()
        {
            if (AutoEval)
            {
                AutoEval = false;
                Eval();
            }

            return base.EngineEval();
        }

        public override EvalResult UserEval()
        {
            Eval();
            return base.UserEval();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("(");

            if (AutoEval)
                sb.Append("! ");

            for (int i = 0; i < Items.Count; i++)
            {
                sb.Append(Items[i].ToString());

                if (i < Items.Count - 1)
                    sb.Append(" ");
            }

            sb.Append(")");

            return sb.ToString();
        }
    }
}
