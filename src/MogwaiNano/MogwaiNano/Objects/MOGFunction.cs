using MogwaiNano.Engine;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;

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
       

        public MOGFunction(MogwaiNanoEngine engine, string content) : base(engine, content)
        {

        }

        public override EvalResult Execute()
        {
            Debug.WriteLine($"EXECUTE {ToString()}");

            Engine.VarPushContext(Name);
            var r = base.Execute();
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
            var obj = new MOGFunction(Engine);

            foreach (MOGObject item in Items)
                obj.Items.Add(item.Clone());

            obj.UpdateFromOther(this);

            return obj;
        }

        public override string ToString() => $"«{ToStringCode()}»";
    }
}
