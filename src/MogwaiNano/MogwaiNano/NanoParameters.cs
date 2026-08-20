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

using nanoFramework.Json;
using System.IO;

namespace MogwaiNano
{
    public class NanoParameters
    {
        public string Name { get; set; } = "MogwaiNanoDevice";

        public static NanoParameters Load(string filename)
        {
            try
            {
                if (File.Exists(filename))
                {
                    var json = File.ReadAllText(filename);
                    var obj = JsonConvert.DeserializeObject(json, typeof(NanoParameters)) as NanoParameters;

                    return obj ?? new NanoParameters();
                }
            }
            catch
            {

            }

            return new NanoParameters();
        }

        public bool Save(string filename)
        {
            try
            {
                string json = JsonConvert.SerializeObject(this);
                File.WriteAllText(AppGlobal.PARAMETERS_FILE, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
