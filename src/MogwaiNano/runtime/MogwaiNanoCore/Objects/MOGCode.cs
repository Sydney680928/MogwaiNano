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
using System.Threading;
using GC = nanoFramework.Runtime.Native.GC;

namespace MogwaiNano.Objects
{
    public class MOGCode : MOGObject
    {
        public ArrayList Items { get; internal set; }

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

        public MOGCode(MogwaiNanoEngine engine, string content, bool automaticMode = true, bool withProgressInformation = false) : this(engine)
        {
            Content = content;
            AutoEval = Content.StartsWith("!");

            if (automaticMode)
                Parse(withProgressInformation);
        }

        public bool Parse(bool withProgressInformations = false)
        {
            try
            {
                if (Content == null)
                    return false;

                var parser = new Parser(Engine);
                Items = parser.Parse(Content, withProgressInformations);
                parser = null;

                if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
                {
                    AutoEval = true;
                    Items.RemoveAt(0);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(Content);

                if (withProgressInformations)
                    Engine.Delegate?.DebugMessage(Engine, ex.Message);

                return false;
            }
        }

        public virtual EvalResult Execute(bool debugCommand = false)
        {
            if (Engine.HaltRequested)
                return EvalResult.Failure(Engine, Error.HaltEncounteredError);

            if (Engine.ExitRequested)
                return EvalResult.NoError;

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

                        if (Engine.BreakRequested || Engine.ExitRequested)
                            break;

                        if (Engine.HaltRequested)
                            return EvalResult.Failure(Engine, Error.HaltEncounteredError);

                        try
                        {
                            var savedCurrentEvalObject = Engine.CurrentEvalObject;

                            if (Engine.FrugalMode)
                            {
                                Engine.CurrentEvalObject = item;
                            }
                            else
                            {
                                Engine.CurrentEvalObject = item.Clone();
                            }

                            if (!debugCommand && Engine.DebugMode && Engine.CurrentEvalObject.DebugInfo != null)
                            {
                                if (Engine.DebugCommand != null)
                                {
                                    Engine.DebugCommand = null;

                                    var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "DEBUG.COMMAND.RESULT", "MOGWAI NANO\nCannot execute command: program is not paused.");
                                    AppGlobal.TcpServer.EnqueueMessage(msg);
                                }

                                if (Engine.PauseRequested)
                                {
                                    Engine.DebugCommand = null;

                                    var debugInfo = Engine.CurrentEvalObject.DebugInfo;
                                    var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "DEBUG.INFO", debugInfo.StartPos.ToString(), debugInfo.EndPos.ToString(), Engine.PauseRequested.ToString(), Engine.NextStepRequested.ToString());
                                    AppGlobal.TcpServer.EnqueueMessage(msg);

                                    while (Engine.PauseRequested)
                                    {
                                        if (Engine.DebugCommand != null)
                                        {
                                            var code = new MOGCode(Engine, Engine.DebugCommand, false);

                                            Engine.DebugCommand = null;

                                            var r = code.Execute(true);

                                            var debugMsg = new ServerMessage(AppGlobal.NanoParameters.Name, "DEBUG.COMMAND.RESULT", r.ToString());
                                            AppGlobal.TcpServer.EnqueueMessage(debugMsg);
                                        }

                                        if (Engine.NextStepRequested)
                                        {
                                            Engine.NextStepRequested = false;
                                            break;
                                        }
                                        else if (Engine.HaltRequested)
                                        {
                                            break;
                                        }

                                        Thread.Sleep(50);
                                    }

                                    Engine.DebugCommand = null;
                                }
                                else if (Engine.DebugTronDelay > 0)
                                {
                                    var debugInfo = Engine.CurrentEvalObject.DebugInfo;
                                    var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "DEBUG.INFO", debugInfo.StartPos.ToString(), debugInfo.EndPos.ToString(), Engine.PauseRequested.ToString(), Engine.NextStepRequested.ToString());
                                    AppGlobal.TcpServer.EnqueueMessage(msg);

                                    Thread.Sleep(Engine.DebugTronDelay);
                                }
                            }

                            result = Engine.CurrentEvalObject.EngineEval();

                            if (debugCommand)
                                Engine.CurrentEvalObject = savedCurrentEvalObject;
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
            var obj = new MOGCode(Engine, Content, false);

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

        public override string ToStringCode()
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
