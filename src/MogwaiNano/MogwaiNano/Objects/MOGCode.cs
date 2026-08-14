using MogwaiNano.Engine;
using System;
using System.Collections;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGCode : MOGObject
    {
        public ArrayList Items { get; } = new();

        public MOGCode(MogwaiNanoEngine engine) : base(engine, engine.TypeCode)
        {

        }   

        public MOGCode(MogwaiNanoEngine engine, ArrayList items) : this(engine)
        {
            Items = items;

            if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
            {
                AutoEval = true;
                Items.RemoveAt(0);
            }
        }

        public MOGCode(MogwaiNanoEngine engine, string content) : this(engine)
        {
            var parser = new Parser(engine);
            Items = parser.Parse(content);

            if (Items.Count > 0 && Items[0] is MOGWord word && word.Value == "!")
            {
                AutoEval = true;
                Items.RemoveAt(0);
            }
        }

        public virtual EvalResult Execute()
        {
            if (Engine.HaltRequested)
                return EvalResult.Failure(Engine, Error.HaltEncounteredError);

            EvalResult result = EvalResult.NoError;

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

                    Engine.CurrentEvalObject = item.Clone();

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
                }
            }
            else
            {
                if (Engine.HasWaitingFireObjects)
                    return Engine.ExecuteWaitingFireObjects();
            }

            return result;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGCode(Engine);

            foreach (MOGObject item in Items)
                obj.Items.Add(item.Clone());

            obj.UpdateFromOther(this);

            return obj;
        }

        public MOGFunction ToFunction()
        {
            var obj = new MOGFunction(Engine, Items);
            obj.UpdateFromOther(this);
            return obj;
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

        public override string ToString() => "{" + ToStringCode() + "}";  
    }
}
