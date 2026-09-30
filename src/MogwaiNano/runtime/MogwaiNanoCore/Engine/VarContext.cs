// Copyright 2026 Stéphane Sibué
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

using MogwaiNano.Objects;
using System.Collections;

namespace MogwaiNano.Engine
{
    public class VarContext
    {
        private Hashtable _vars = new();

        public string Name { get; init; }

        public string[] Keys
        {
            get
            {
                var keys = new string[_vars.Keys.Count];
                _vars.Keys.CopyTo(array: keys, 0);
                return keys;
            }
        }

        public VarContext(string name)
        {
            Name = name;
        }

        public void Clear()
        {
            _vars.Clear();
        }

        public bool Write(string name, MOGObject value)
        {
            if (_vars.Contains(name))
            {
                var v = _vars[name] as Var;
                v.Value = value;
                return true;
            }
            else
            {
                _vars[name] = new Var(name, value);
                return true;
            }
        }

        public MOGObject Read(string name, bool clone = true)
        {
            if (_vars.Contains(name))
            {
                var v = _vars[name] as Var; 
                return clone ? v.Value.Clone() : v.Value;
            }

            return null;
        }

        public bool Exists(string name) => _vars.Contains(name);

        public bool Purge(string name)
        {
            if (_vars.Contains(name))
            {
                _vars.Remove(name);
                return true;
            }

            return false;
        }
    }
}
