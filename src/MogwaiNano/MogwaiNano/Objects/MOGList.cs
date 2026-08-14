using MogwaiNano.Engine;
using System.Collections;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGList : MOGObject
    {
        public ArrayList Items { get; } = new();

        public MOGList(MogwaiNanoEngine engine) : base(engine, engine.TypeList)
        {

        }   

        public MOGList(MogwaiNanoEngine engine, ArrayList items) : this(engine)
        {
            Items = items;
        }

        public MOGList(MogwaiNanoEngine engine, string content) : this(engine)
        {
            var parser = new Parser(engine);
            Items = parser.Parse(content);
        }

        public MOGObject GetItem(int index)
        {
            if (index >= 0 && index < Items.Count)
                return Items[index] as MOGObject;

            return null;
        }

        public void AddItem(MOGObject item) => Items.Add(item);

        public bool SetItem(int index, MOGObject value)
        {
            if (index >= 0 && index < Items.Count)
            {
                Items[index] = value;
                return true;
            }

            return false;
        }   

        public override MOGObject Clone()
        {
            var obj = new MOGList(Engine);

            foreach (MOGObject item in Items)
                obj.Items.Add(item.Clone());

            obj.UpdateFromOther(this);

            return obj;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("(");

            if (AutoEval)
                sb.Append("! ");

            for (int i = 0; i < Items.Count; i++)
            {
                sb.Append(Items[i].ToString());

                if (i < Items.Count - 1)
                    sb.Append(" ");
            }

            sb.Append(")");

            return sb.ToString();
        }
    }
}
