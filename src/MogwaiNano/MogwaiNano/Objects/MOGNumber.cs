using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGNumber : MOGObject
    {
        public float Value { get; set; }
        
        public MOGNumber(MogwaiNanoEngine engine, float value) : base(engine, engine.TypeNumber)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGNumber(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
