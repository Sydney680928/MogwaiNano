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
            obj.UpdateFromOther(this);
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
