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

using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGNumber : MOGObject
    {
        private float _value;
        private double _doubleValue;

        public float Value { 
            
            get => _value;

            set
            {
                _doubleValue = value;
                _value = value;
            }
        }

        public double DoubleValue => _doubleValue;

        public MOGNumber(MogwaiNanoEngine engine, double value) : base(engine, engine.TypeNumber)
        {
            _value = (float)value;
            _doubleValue = value;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGNumber(Engine, _doubleValue);

            if (DebugInfo != null)
                obj.DebugInfo = DebugInfo.Clone();

            return obj;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override string ToSmartString()
        {
            var s = Value.ToString();
            var size = s.Length.ToString("X2");
            return $"N{size}{s}";
        }
    }
}
