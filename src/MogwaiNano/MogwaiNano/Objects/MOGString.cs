using MogwaiNano.Engine;

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
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return $"\"{Value}\"";
        }
    }
}
