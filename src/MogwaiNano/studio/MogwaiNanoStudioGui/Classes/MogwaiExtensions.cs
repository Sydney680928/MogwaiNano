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

using Avalonia.Controls.Templates;
using Avalonia.Media;
using HarfBuzzSharp;
using MOGWAI;
using MOGWAI.Engine;
using MOGWAI.Objects;
using System;
using System.Collections.Generic;
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

        public static string ToSmartString(this MOGObject obj)
        {
            if (obj is MOGNumber number)
            {
                var s = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var size = s.Length.ToString("X2");
                return $"N{size}{s}";
            }
            else if (obj is MOGString @string)
            {
                var size = @string.Value.ToString().Length.ToString("X4");
                return $"S{size}{@string.Value}";
            }
            else if (obj is MOGName name)
            {
                var size = name.Value.Length.ToString("X2");
                return $"A{size}{name.Value}";
            }
            else if (obj is MOGKey key)
            {
                var size = key.Value.Length.ToString("X2");
                return $"K{size}{key.Value}";
            }
            else if (obj is MOGPrimitive primitive)
            {
                var size = primitive.Name.Length.ToString("X2");
                return $"P{size}{primitive.Name}";
            }
            else if (obj is MOGWord word)
            {
                var size = word.Value.Length.ToString("X2");
                return $"W{size}{word.Value}";
            }
            else if (obj is MOGHostFunction hostFunction)
            {
                var size = hostFunction.Value.Length.ToString("X2");
                return $"W{size}{hostFunction.Value}";
            }
            else if (obj is MOGRef reference)
            {
                var size = reference.Value.Length.ToString("X2");
                return $"&{size}{reference.Value}";
            }
            else if (obj is MOGBoolean boolean)
            {
                return $"B{(boolean.Value ? "1" : "0")}";
            }
            else if (obj is MOGType type)
            {
                var size = type.Value.Length.ToString("X2");
                return $"T{size}{type.Value}";
            }
            else if (obj is MOGNull @null)
            {
                return "X";
            }
            else if (obj is MOGData data)
            {
                var sb = new StringBuilder();

                for (int i = 0; i < data.Items.Count; i++)
                    sb.Append((data.Items[i].ToString("X2")));

                var size = sb.Length.ToString("X4");
                return $"D{size}{sb}";
            }
            else if (obj is MOGFunction function)
            {
                var sb = new StringBuilder();

                foreach (var item in function.Items)
                    sb.Append(item.ToSmartString());

                var size = sb.Length.ToString("X4");

                return $"F{size}{sb}";
            }
            else if (obj is MOGCode code)
            {
                var sb = new StringBuilder();

                foreach (var item in code.Items)
                    sb.Append(item.ToSmartString());

                var size = sb.Length.ToString("X4");
                var header = code.AutoEval ? "C" : "c";

                return $"{header}{size}{sb}";
            }
            else if (obj is MOGList list)
            {
                var sb = new StringBuilder();

                foreach (var item in list.Items)
                    sb.Append(item.ToSmartString());

                var size = sb.Length.ToString("X4");
                var header = list.AutoEval ? "L" : "l";

                return $"{header}{size}{sb}";
            }
            else if (obj is MOGRecord record)
            {
                var sb = new StringBuilder();

                foreach (var k in record.Items.Keys)
                {
                    var mKey = new MOGKey(record.Engine, k);
                    sb.Append(mKey.ToSmartString());
                    sb.Append(record.Items[k].ToSmartString());
                }

                var size = sb.Length.ToString("X4");
                var header = record.AutoEval ? "R" : "r";

                return $"{header}{size}{sb}";
            }

            return obj.ToString();
        }

        public static string ToSmartDebugAllString(this MOGObject obj)
        {
            string debugString;

            if (obj.StartPos == -1)
            {
                debugString = "FFFF0000";
            }
            else
            {
                debugString = obj.StartPos.ToString("X4") + obj.EndPos.ToString("X4");
            }

            if (obj is MOGNumber number)
            {
                var s = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var size = s.Length.ToString("X2");
                return $"N{size}{debugString}{s}";
            }
            else if (obj is MOGString @string)
            {
                var size = @string.Value.ToString().Length.ToString("X4");
                return $"S{size}{debugString}{@string.Value}";
            }
            else if (obj is MOGName name)
            {
                var size = name.Value.Length.ToString("X2");
                return $"A{size}{debugString}{name.Value}";
            }
            else if (obj is MOGKey key)
            {
                var size = key.Value.Length.ToString("X2");
                return $"K{size}{debugString}{key.Value}";
            }
            else if (obj is MOGPrimitive primitive)
            {
                var size = primitive.Name.Length.ToString("X2");
                return $"P{size}{debugString}{primitive.Name}";
            }
            else if (obj is MOGWord word)
            {
                var size = word.Value.Length.ToString("X2");
                return $"W{size}{debugString}{word.Value}";
            }
            else if (obj is MOGHostFunction hostFunction)
            {
                var size = hostFunction.Value.Length.ToString("X2");
                return $"W{size}{debugString}{hostFunction.Value}";
            }
            else if (obj is MOGRef reference)
            {
                var size = reference.Value.Length.ToString("X2");
                return $"&{size}{debugString}{reference.Value}";
            }
            else if (obj is MOGBoolean boolean)
            {
                return $"B{debugString}{(boolean.Value ? "1" : "0")}";
            }
            else if (obj is MOGType type)
            {
                var size = type.Value.Length.ToString("X2");
                return $"T{size}{debugString}{type.Value}";
            }
            else if (obj is MOGNull @null)
            {
                return $"X{debugString}";
            }
            else if (obj is MOGData data)
            {
                var sb = new StringBuilder();

                for (int i = 0; i < data.Items.Count; i++)
                    sb.Append((data.Items[i].ToString("X2")));

                var size = sb.Length.ToString("X4");
                return $"D{size}{debugString}{sb}";
            }
            else if (obj is MOGFunction function)
            {
                var sb = new StringBuilder();

                foreach (var item in function.Items)
                    sb.Append(item.ToSmartDebugAllString());

                var size = sb.Length.ToString("X4");

                return $"F{size}{debugString}{sb}";
            }
            else if (obj is MOGCode code)
            {
                var sb = new StringBuilder();

                foreach (var item in code.Items)
                    sb.Append(item.ToSmartDebugAllString());

                var size = sb.Length.ToString("X4");
                var header = code.AutoEval ? "C" : "c";

                return $"{header}{size}{debugString}{sb}";
            }
            else if (obj is MOGList list)
            {
                var sb = new StringBuilder();

                foreach (var item in list.Items)
                    sb.Append(item.ToSmartDebugAllString());

                var size = sb.Length.ToString("X4");
                var header = list.AutoEval ? "L" : "l";

                return $"{header}{size}{debugString}{sb}";
            }
            else if (obj is MOGRecord record)
            {
                var sb = new StringBuilder();

                foreach (var k in record.Items.Keys)
                {
                    var mKey = new MOGKey(record.Engine, k);
                    sb.Append(mKey.ToSmartDebugAllString());
                    sb.Append(record.Items[k].ToSmartDebugAllString());
                }

                var size = sb.Length.ToString("X4");
                var header = record.AutoEval ? "R" : "r";

                return $"{header}{size}{debugString}{sb}";
            }

            return obj.ToString();
        }

        public static string ToSmartDebugString(this MOGObject obj)
        {
            string debugString;

            if (obj.StartPos == -1)
            {
                debugString = "FFFF0000";
            }
            else
            {
                debugString = obj.StartPos.ToString("X4") + obj.EndPos.ToString("X4");
            }

            if (obj is MOGNumber number)
            {
                var s = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var size = s.Length.ToString("X2");
                return $"N{size}FFFF0000{s}";
            }
            else if (obj is MOGString @string)
            {
                var size = @string.Value.ToString().Length.ToString("X4");
                return $"S{size}FFFF0000{@string.Value}";
            }
            else if (obj is MOGName name)
            {
                var size = name.Value.Length.ToString("X2");
                return $"A{size}FFFF0000{name.Value}";
            }
            else if (obj is MOGKey key)
            {
                var size = key.Value.Length.ToString("X2");
                return $"K{size}FFFF0000{key.Value}";
            }
            else if (obj is MOGPrimitive primitive)
            {
                var size = primitive.Name.Length.ToString("X2");
                return $"P{size}{debugString}{primitive.Name}";
            }
            else if (obj is MOGWord word)
            {
                var size = word.Value.Length.ToString("X2");
                return $"W{size}{debugString}{word.Value}";
            }
            else if (obj is MOGHostFunction hostFunction)
            {
                var size = hostFunction.Value.Length.ToString("X2");
                return $"W{size}{debugString}{hostFunction.Value}";
            }
            else if (obj is MOGRef reference)
            {
                var size = reference.Value.Length.ToString("X2");
                return $"&{size}{debugString}{reference.Value}";
            }
            else if (obj is MOGBoolean boolean)
            {
                return $"BFFFF0000{(boolean.Value ? "1" : "0")}";
            }
            else if (obj is MOGType type)
            {
                var size = type.Value.Length.ToString("X2");
                return $"T{size}FFFF0000{type.Value}";
            }
            else if (obj is MOGNull)
            {
                return $"XFFFF0000";
            }
            else if (obj is MOGData data)
            {
                var sb = new StringBuilder();

                for (int i = 0; i < data.Items.Count; i++)
                    sb.Append((data.Items[i].ToString("X2")));

                var size = sb.Length.ToString("X4");
                return $"D{size}FFFF0000{sb}";
            }
            else if (obj is MOGFunction function)
            {
                var sb = new StringBuilder();

                foreach (var item in function.Items)
                    sb.Append(item.ToSmartDebugString());

                var size = sb.Length.ToString("X4");

                if (function.PauseAllowed)
                    return $"F{size}{debugString}{sb}";

                return $"F{size}FFFF0000{sb}";
            }
            else if (obj is MOGCode code)
            {
                var sb = new StringBuilder();

                foreach (var item in code.Items)
                    sb.Append(item.ToSmartDebugString());

                var size = sb.Length.ToString("X4");
                var header = code.AutoEval ? "C" : "c";

                if (code.PauseAllowed)
                    return $"{header}{size}{debugString}{sb}";

                return $"{header}{size}FFFF0000{sb}";
            }
            else if (obj is MOGList list)
            {
                var sb = new StringBuilder();

                foreach (var item in list.Items)
                    sb.Append(item.ToSmartDebugString());

                var size = sb.Length.ToString("X4");

                if (list.AutoEval)
                    return $"L{size}{debugString}{sb}";

                return $"l{size}FFFF0000{sb}";
            }
            else if (obj is MOGRecord record)
            {
                var sb = new StringBuilder();

                foreach (var k in record.Items.Keys)
                {
                    var mKey = new MOGKey(record.Engine, k);
                    sb.Append(mKey.ToSmartDebugString());
                    sb.Append(record.Items[k].ToSmartDebugString());
                }

                var size = sb.Length.ToString("X4");
                
                if (record.AutoEval)
                    return $"R{size}{debugString}{sb}"; 

                return $"r{size}FFFF0000{sb}";
            }

            return "***???***";
        }
    }
}
