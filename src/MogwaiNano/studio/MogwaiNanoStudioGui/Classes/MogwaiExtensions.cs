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

using Avalonia.Media;
using HarfBuzzSharp;
using MOGWAI;
using MOGWAI.Objects;
using System.Text;

namespace MogwaiNanoStudioGui.Classes
{
    internal static class MogwaiExtensions
    {
        public static string ToDebugString(this MOGObject obj)
        {
            if (obj is MOGPrimitive primitive)
            {
                if (primitive.PauseAllowed)
                    return $"{primitive}§{primitive!.StartPos};{primitive!.EndPos}";

                return primitive.ToString();
            }
            else if (obj is MOGWord word)
            {
                if (word.PauseAllowed)
                    return $"{word}§{word!.StartPos};{word!.EndPos}";

                return word.ToString();
            }
            else if (obj is MOGRef reference)
            {
                if (reference.PauseAllowed)
                    return $"{reference}§{reference!.StartPos};{reference!.EndPos}";

                return reference.ToString();
            }
            else if (obj is MOGList list && list.AutoEval)
            {
                var sb = new StringBuilder();

                sb.Append("(");

                if (list!.AutoEval)
                    sb.Append("! ");

                for (int i = 0; i < list!.Items.Count; i++)
                {
                    sb.Append(list!.Items[i].ToDebugString());

                    if (i < list!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append(")");

                if (list.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGRecord record && record.AutoEval)
            {
                var sb = new StringBuilder();

                sb.Append("[");

                if (record!.AutoEval)
                    sb.Append("! ");

                foreach (var key in record!.Items.Keys)
                {
                    if (sb.Length > 0)
                        sb.Append(" ");

                    sb.Append(key);
                    sb.Append(": ");
                    sb.Append(record.Items[key].ToDebugString());
                }

                sb.Append("]");

                if (record.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGFunction function)
            {
                var sb = new StringBuilder();

                sb.Append("«");

                for (int i = 0; i < function!.Items.Count; i++)
                {
                    sb.Append(function!.Items[i].ToDebugString());

                    if (i < function!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append("»");

                if (function.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGCode code)
            {
                var sb = new StringBuilder();

                sb.Append("{");

                if (code!.AutoEval)
                    sb.Append("! ");

                for (int i = 0; i < code!.Items.Count; i++)
                {
                    sb.Append(code!.Items[i].ToDebugString());

                    if (i < code!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append("}");

                if (code.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else
            {
                return obj.ToString();
            }
        }

        public static string ToDebugAllString(this MOGObject obj)
        {
            if (obj is MOGPrimitive primitive)
            {
                if (primitive.PauseAllowed)
                    return $"{primitive}§{primitive!.StartPos};{primitive!.EndPos}";

                return primitive.ToString();
            }
            else if (obj is MOGWord word)
            {
                if (word.PauseAllowed)
                    return $"{word}§{word!.StartPos};{word!.EndPos}";

                return word.ToString();
            }
            else if (obj is MOGRef reference)
            {
                if (reference.PauseAllowed)
                    return $"{reference}§{reference!.StartPos};{reference!.EndPos}";

                return reference.ToString();
            }
            else if (obj is MOGList list)
            {
                var sb = new StringBuilder();

                sb.Append("(");

                if (list!.AutoEval)
                    sb.Append("! ");

                for (int i = 0; i < list!.Items.Count; i++)
                {
                    sb.Append(list!.Items[i].ToDebugAllString());

                    if (i < list!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append(")");

                if (list.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGRecord record)
            {
                var sb = new StringBuilder();

                sb.Append("[");

                if (record!.AutoEval)
                    sb.Append("! ");

                foreach (var key in record!.Items.Keys)
                {
                    if (sb.Length > 0)
                        sb.Append(" ");

                    sb.Append(key);
                    sb.Append(": ");
                    sb.Append(record.Items[key].ToDebugAllString());
                }

                sb.Append("]");

                if (record.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGFunction function)
            {
                var sb = new StringBuilder();

                sb.Append("«");

                for (int i = 0; i < function!.Items.Count; i++)
                {
                    sb.Append(function!.Items[i].ToDebugAllString());

                    if (i < function!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append("»");

                if (function.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else if (obj is MOGCode code)
            {
                var sb = new StringBuilder();

                sb.Append("{");

                if (code!.AutoEval)
                    sb.Append("! ");

                for (int i = 0; i < code!.Items.Count; i++)
                {
                    sb.Append(code!.Items[i].ToDebugAllString());

                    if (i < code!.Items.Count - 1)
                        sb.Append(" ");
                }

                sb.Append("}");

                if (code.PauseAllowed)
                    sb.Append($"§{obj.StartPos};{obj.EndPos}");

                return sb.ToString();
            }
            else
            {
                if (obj.PauseAllowed)               
                    return $"{obj}§{obj.StartPos};{obj.EndPos}";

                return obj.ToString();
            }
        }
    }
}
