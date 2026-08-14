// Copyright 2015-2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MogwaiNano.Engine;
using MogwaiNano.Exceptions;
using nanoFramework.Json.Configuration;
using System.Collections;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGData : MOGObject
    {
        public ArrayList Items { get; } = new();

        public MOGData(MogwaiNanoEngine engine) : base(engine, engine.TypeData)
        {
           
        }

        public MOGData(MogwaiNanoEngine engine, ArrayList items) : this(engine)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not byte)
                    throw new MogwaiInvalidDataException("items must be a collection of bytes.");
                
                Items.Add(items[i]);
            }
        }

        public MOGData(MogwaiNanoEngine engine, byte[] items) : this(engine)
        {
            for (int i = 0; i < items.Length; i++)
                Items.Add(items[i]);
        }

        public MOGData(MogwaiNanoEngine engine, string content) : this(engine)
        {
            // content = FF45AE12
            // taille paire
            // Composée QUE de valeurs hexa sur 2 caractères

            if (content.Length % 2 != 0)
                throw new MogwaiInvalidDataException("content must be a collection of hex bytes.");

            var bytes = new ArrayList();

            for (int i = 0; i < content.Length; i += 2)
            {
                try
                {
                    var v = System.Convert.ToByte(content.Substring(i, 2), 16);
                    bytes.Add(v);
                }
                catch
                {
                    throw new MogwaiInvalidRecordException("content must be a collection of hex bytes.");
                }              
            }

            Items = bytes;
        }

        public EvalResult RemoveItem(int index)
        {
            if (index < 0 || index >= Items.Count)
                return EvalResult.Failure(Engine, Error.BadArgumentValueError);

            Items.RemoveAt(index);
            return EvalResult.NoError;
        }

        public byte GetItem(int index)
        {
            if (index >= 0 && index < Items.Count)
                return (byte)Items[index];

            return 0;
        }

        public void AddItem(byte item) => Items.Add(item);

        public bool SetItem(int index, byte value)
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
            var obj = new MOGData(Engine, Items);
            obj.UpdateFromOther(this);   
            return obj;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var item in Items)        
                sb.Append(string.Format("{0:X2}", (byte)item));   

            return $"D:{sb.ToString()}";
        }
    }
}
