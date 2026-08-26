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
using System;
using System.Text;

namespace MogwaiNano.Objects
{
    public class MOGData : MOGObject
    {
        public byte[] Items { get; set; } = new byte[0];

        public MOGData(MogwaiNanoEngine engine) : base(engine, engine.TypeData)
        {

        }

        public MOGData(MogwaiNanoEngine engine, byte[] items) : this(engine)
        {
            Items = items;
        }

        public MOGData(MogwaiNanoEngine engine, string content) : this(engine)
        {
            // content = FF45AE12
            // taille paire
            // Composée QUE de valeurs hexa sur 2 caractères

            if (content.Length % 2 != 0)
                throw new MogwaiInvalidDataException("content must be a collection of hex bytes.");

            Items = new byte[content.Length / 2];
            var index = 0;

            for (int i = 0; i < content.Length; i += 2)
            {
                try
                {
                    var v = System.Convert.ToByte(content.Substring(i, 2), 16);
                    Items[index++] = v;
                }
                catch
                {
                    throw new MogwaiInvalidRecordException("content must be a collection of hex bytes.");
                }
            }
        }

        public byte GetItem(int index)
        {
            if (index >= 0 && index < Items.Length)
                return Items[index];

            return 0;
        }

        public void AddItem(byte item)
        {
            var newItems = new byte[Items.Length + 1];
            Array.Copy(Items, newItems, Items.Length);
            newItems[Items.Length] = item;
            Items = newItems;
        }

        public bool SetItem(int index, byte value)
        {
            if (index >= 0 && index < Items.Length)
            {
                Items[index] = value;
                return true;
            }

            return false;
        }

        public override MOGObject Clone()
        {
            var newItems = new byte[Items.Length];
            Array.Copy(Items, newItems, Items.Length);

            var obj = new MOGData(Engine, newItems);           
            return obj;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var item in Items)
                sb.Append(string.Format("{0:X2}", (byte)item));

            return $"D:{sb.ToString()}";
        }

        public SpanByte ToSpanByte() => new SpanByte(Items);
    }
}
