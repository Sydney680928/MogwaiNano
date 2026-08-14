using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGKey : MOGObject
    {
        public string Value { get; set; }
        
        public MOGKey(MogwaiNanoEngine engine, string value) : base(engine, engine.TypeKey)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGKey(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return $"{Value}:";
        }
    }
}
