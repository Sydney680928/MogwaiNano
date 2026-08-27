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
using MogwaiNano.Interfaces;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using GC = nanoFramework.Runtime.Native.GC;

namespace MogwaiNano.Objects
{
    public class MOGCode : MOGObject
    {
        public ArrayList Items { get; private set; }

        public string Content { get; protected set; }

        public MOGCode(MogwaiNanoEngine engine) : base(engine, engine.TypeCode)
        {

        }   

        public MOGCode(MogwaiNanoEngine engine, ArrayList items) : this(engine)
        {
            Items = items;
            Content = ToStringCode();

            if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
            {
                AutoEval = true;
                Items.RemoveAt(0);
            }
        }

        public MOGCode(MogwaiNanoEngine engine, string content) : this(engine)
        {
            Content = content;
        }

        public bool Parse()
        {
            try
            {
                if (Content == null)
                    return false;

                var parser = new Parser(Engine);
                Items = parser.Parse(Content);
                parser = null;

                if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
                {
                    AutoEval = true;
                    Items.RemoveAt(0);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public virtual EvalResult Execute()
        {
            if (Engine.HaltRequested)
                return EvalResult.Failure(Engine, Error.HaltEncounteredError);

            if (Content == null && Items == null)
                return EvalResult.Failure(Engine, Error.FatalError, "unabled to execute code, content and items are empty");    

            EvalResult result = EvalResult.NoError;

            try
            {
                if (Items == null)
                {
                    if (!Parse())
                        return EvalResult.Failure(Engine, Error.ParseError);
                }

                if (Items.Count > 0)
                {
                    foreach (MOGObject item in Items)
                    {
                        if (result != EvalResult.NoError)
                            break;

                        if (Engine.HasWaitingFireObjects)
                            result = Engine.ExecuteWaitingFireObjects();

                        if (result != EvalResult.NoError)
                            break;

                        if (Engine.BreakRequested)
                            break;

                        if (Engine.HaltRequested)
                            return EvalResult.Failure(Engine, Error.HaltEncounteredError);

                        if (Engine.FrugalMode)
                        {
                            Engine.CurrentEvalObject = item;
                        }
                        else
                        {
                            Engine.CurrentEvalObject = item.Clone();
                        }

                        try
                        {
                            result = Engine.CurrentEvalObject.EngineEval();
                        }
                        catch (Exception ex)
                        {
                            result = EvalResult.Failure(Engine, Error.FatalError, ex.Message);
                        }

                        if (result.IsError)
                            break;

                        Engine.Idle();
                    }
                }
                else
                {
                    if (Engine.HasWaitingFireObjects)
                    {
                        return Engine.ExecuteWaitingFireObjects();
                    }
                    else
                    {
                        Engine.Idle();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(Engine, Error.FatalError, ex.Message);
            }
            finally
            {
                if (Engine.FrugalMode)
                    Items = null;
            }
        }

        public override MOGObject Clone()
        {
            var obj = new MOGCode(Engine, Content);

            if (Engine.FrugalMode)
            {
                obj.AutoEval = Content.StartsWith("!");
            }
            else
            {
                if (Items != null)
                {
                    foreach (MOGObject item in Items)
                        obj.Items.Add(item.Clone());
                }
            }

            return obj;
        }

        public MOGFunction ToFunction()
        {
            if (Engine.FrugalMode)
            {
                var obj = new MOGFunction(Engine, Content);
                return obj;
            }
            else
            {
                var obj = new MOGFunction(Engine, Items);

                if (Items != null)
                {                   
                    obj.AutoEval = AutoEval;
                    obj.Content = Content;
                }

                return obj;
            }
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

        public override EvalResult UserEval() => Execute();

        public string ToStringCode()
        {
            if (Items != null)
            {
                var sb = new StringBuilder();

                if (AutoEval)
                    sb.Append("! ");

                for (int i = 0; i < Items.Count; i++)
                {
                    sb.Append(Items[i].ToString());

                    if (i < Items.Count - 1)
                    {
                        sb.Append(" ");
                    }
                }

                return sb.ToString();
            }
            else if (Content != null)
            {
                return Content;
            }

            return "*** !!! ***";
        }

        public override string ToString() => "{" + ToStringCode() + "}";
    }
}
