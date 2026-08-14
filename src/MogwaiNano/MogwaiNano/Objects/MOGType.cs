using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGType : MOGObject
    {
        public string Value { get; set; }
        
        public MOGType(MogwaiNanoEngine engine, string value) : base(engine, engine.TypeType)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGType(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return $".{Value}";
        }
    }
}
