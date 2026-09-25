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
    public class MOGString : MOGObject
    {
        public string Value { get; set; }
        
        public MOGString(MogwaiNanoEngine engine, string value) : base(engine, engine.TypeString)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGString(Engine, Value);
            return obj;
        }

        public override EvalResult UserEval()
        {
            var result = Eval();

            if (result.IsError)
                return result;

            return base.UserEval();
        }

        public EvalResult Eval()
        {
            // name = "Stéphane" (string)
            // age = 57 (number)
            // "Hello {! name} you are {! age}" ---> "Hello Stéphane you are 57"

            var items = ParseStringFormat();

            if (items != null)
            {
                var sb = new StringBuilder();

                foreach (var item in items)
                {
                    if (item is MOGString s)
                    {
                        sb.Append(s.Value);
                    }
                    else if (item is MOGCode c)
                    {
                        if (c.AutoEval)
                        {
                            try
                            {
                                Engine.AddNewStack();

                                var result = c.Execute();

                                if (result != EvalResult.NoError)
                                    return result;

                                if (Engine.StackSize != 1)
                                    return EvalResult.Failure(Engine, Error.StackSizeError, "stack size differs from 1 during string eval.");

                                var obj = Engine.StackPop();

                                if (obj == null)
                                    return EvalResult.Failure(Engine, Error.StackSizeError, "unabled to get stack value during string eval.");
                            
                                if (obj is MOGString s2)
                                {
                                    sb.Append(s2.Value);
                                }
                                else
                                {
                                    sb.Append(obj.ToString());
                                }
                            }
                            finally
                            {
                                Engine.RemoveLastStack();
                            }
                        }
                        else
                        {
                            sb.Append(item.ToString());
                        }
                    }
                }

                Value = sb.ToString();

                return EvalResult.NoError;
            }

            return EvalResult.NoError;
        }

        private ArrayList ParseStringFormat()
        {
            // "Hello {! name} you are {! age}" ---> "Hello " {! name} " you are " {! age} = 4 items

            int index = 0;
            ArrayList items = new();
            StringBuilder currentItem = new();
            bool inCode = false;

            while (index < Value.Length)
            {
                var c = Value[index++];

                if (c == '{')
                {
                    if (inCode)
                        return null;

                    inCode = true;

                    if (currentItem.Length > 0)
                    {
                        var s = new MOGString(Engine, currentItem.ToString());
                        items.Add(s);
                        currentItem.Clear();
                    }
                }
                else if (c == '}')
                {
                    if (!inCode)
                        return null;

                    var code = new MOGCode(Engine, currentItem.ToString());

                    items.Add(code);
                    currentItem.Clear();
                    inCode = false;
                }
                else
                {
                    currentItem.Append(c);
                }
            }

            if (currentItem.Length > 0)
            {
                var s = new MOGString(Engine, currentItem.ToString());
                items.Add(s);
            }

            return items;
        }

        public override string ToString()
        {
            return $"\"{Value}\"";
        }
    }
}
