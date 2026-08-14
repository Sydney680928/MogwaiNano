using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGBoolean : MOGObject
    {
        public bool Value { get; set; }
        
        public MOGBoolean(MogwaiNanoEngine engine, bool value) : base(engine, engine.TypeBoolean)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGBoolean(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString() => Value ? "true" : "false";    

    }
}
