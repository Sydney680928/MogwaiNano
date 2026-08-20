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

using System.Text;

namespace MogwaiNanoStudio
{
    public class ServerMessage
    {
        const char FIELD_SEPARATOR = '\x1E';

        public string Source { get; set; }
        
        public string Function { get; set; }
        
        public string[] Parameters { get; set; }

        public ServerMessage()
        {
            Source = string.Empty;
            Function = string.Empty;
            Parameters = new string[0];
        }

        public ServerMessage(string source, string function, params string[] parameters)
        {
            Source = source;
            Function = function;
            Parameters = parameters;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("MESSAGE FROM: ");
            sb.AppendLine(Source);

            sb.Append("FUNCTION: ");    
            sb.AppendLine(Function);

            foreach (var param in Parameters)
            {
                sb.Append("-");
                sb.AppendLine(param);
            }

            return sb.ToString();
        }

        public string ToNanoFormat()
        {
            var sb = new StringBuilder();

            sb.Append(Source);
            sb.Append(FIELD_SEPARATOR);
            sb.Append(Function);

            foreach (var param in Parameters)
            {
                sb.Append(FIELD_SEPARATOR);
                sb.Append(param);
            }

            return sb.ToString();
        }

        public static ServerMessage FromNanoFormat(string raw)
        {
            string[] fields = raw.Split(FIELD_SEPARATOR);

            if (fields.Length < 2)
                return null;

            string source = fields[0];
            string function = fields[1];
            string[] parameters = new string[fields.Length - 2];

            for (int i = 2; i < fields.Length; i++)
                parameters[i - 2] = fields[i];

            return new ServerMessage(source, function, parameters);
        }
    }
}
