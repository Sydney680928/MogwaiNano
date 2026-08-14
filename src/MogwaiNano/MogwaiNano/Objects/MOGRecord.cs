using MogwaiNano.Engine;
using System.Collections;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGRecord : MOGObject
    {
        public Hashtable Items { get; } = new();

        public MOGRecord(MogwaiNanoEngine engine) : base(engine, engine.TypeRecord)
        {

        }   

        public MOGRecord(MogwaiNanoEngine engine, Hashtable items) : this(engine)
        {
            Items = items;
        }

        public MOGRecord(MogwaiNanoEngine engine, string content) : this(engine)
        {
            var parser = new Parser(engine);
            var items = parser.Parse(content);

            if (items.Count % 2 != 0)
                throw new System.Exception("items must be in pairs");

            for (int i = 0; i < items.Count; i += 2)
            {
                var key = items[i] as MOGKey;
                var value = items[i + 1] as MOGObject;
                
                if (key == null || value == null)
                    throw new System.Exception("items must be in pairs of MOGKey and MOGObject");
                
                Items.Add(key.Value, value);
            }   
        }

        public MOGObject GetItem(string key)
        {
            if (Items.Contains(key))    
                return Items[key] as MOGObject;
            
            return null;    
        }

        public void SetItem(string key, MOGObject value)
        {
            if (Items.Contains(key))
            {
                Items[key] = value;
            }
            else
            {
                Items.Add(key, value);
            }   
        }

        public override MOGObject Clone()
        {
            var obj = new MOGRecord(Engine);

            foreach (var key in Items.Keys)
            {
                var value = Items[key] as MOGObject;
                obj.Items.Add(key, value.Clone());
            }

            obj.UpdateFromOther(this);

            return obj;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (AutoEval)
                sb.Append("!");

            foreach (var key in Items.Keys)
            {
                if (sb.Length > 0)
                    sb.Append(" ");

                sb.Append(key);
                sb.Append(": ");
                sb.Append(Items[key]);
            }

            return $"[{sb}]";
        }
    }
}
