using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGName : MOGObject
    {
        public string Value { get; set; }
        
        public MOGName(MogwaiNanoEngine engine, string value) : base(engine, engine.TypeName)
        {
            Value = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGName(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return $"'{Value}'";
        }
    }
}
