using MogwaiNano.Objects;

namespace MogwaiNano.Engine
{
    public class Var
    {
        public string Name { get; set; }

        public MOGObject Value { get; set; }

        public Var(string name, MOGObject value)
        {
            Name = name;
            Value = value;
        }
    }
}
