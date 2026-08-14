using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGNull : MOGObject
    {
        public MOGNull(MogwaiNanoEngine engine) : base(engine, engine.TypeNull)
        {

        }

        public override MOGObject Clone()
        {
            var obj = new MOGNull(Engine);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return "null";
        }
    }
}
