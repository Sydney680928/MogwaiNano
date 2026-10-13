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

using MogwaiNano.Exceptions;
using MogwaiNano.Objects;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace MogwaiNano.Engine
{
    public class Parser
    {
        private string _code;
        private ArrayList _items;
        private int _index;
        private MogwaiNanoEngine _engine;
        private StringBuilder _item;
        private string _debugInfo;

        public Parser(MogwaiNanoEngine engine)
        {
            _engine = engine;
        }

        public ArrayList Parse(string code, bool withProgressInformations = false)
        {
            _items = new();
            _index = 0;
            _code = code;
            _item = new();

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (_index < _code.Length)
            {
                char c = _code[_index++];

                if (c == ' ')
                {
                    if (_item.Length > 0)
                    {
                        var parsedItem = ParseItem(_item.ToString());
                        _items.Add(parsedItem);
                        _item.Clear();
                    }

                    continue;
                }
                else if (c == '"')
                {
                    var parts = GetEnclosedItem('"', '"');
                    var mogString = new MOGString(_engine, parts[0]);

                    if (parts.Length > 1)
                        mogString.DebugInfo = new DebugInformations(parts[1]);  
                    
                    _items.Add(mogString);
                    _item.Clear();
                }
                else if (c == '\'')
                {
                    var parts = GetEnclosedItem('\'', '\'');
                    var mogName = new MOGName(_engine, parts[0]);

                    if (parts.Length > 1)
                        mogName.DebugInfo = new DebugInformations(parts[1]);

                    _items.Add(mogName);
                    _item.Clear();
                }
                else if (c == '(')
                {
                    var parts = GetEnclosedItem('(', ')');
                    var mogList = new MOGList(_engine, parts[0]);

                    if (parts.Length > 1)
                        mogList.DebugInfo = new DebugInformations(parts[1]);

                    _items.Add(mogList);
                    _item.Clear();
                }
                else if (c == '[')
                {
                    var parts = GetEnclosedItem('[', ']');
                    var mogRecord = new MOGRecord(_engine, parts[0]);

                    if (parts.Length > 1)
                        mogRecord.DebugInfo = new DebugInformations(parts[1]);

                    _items.Add(mogRecord);
                    _item.Clear();
                }
                else if (c == '{')
                {
                    var parts = GetEnclosedItem('{', '}');
                    var mogCode = new MOGCode(_engine, parts[0]);

                    if (parts.Length > 1)
                        mogCode.DebugInfo = new DebugInformations(parts[1]);

                    _items.Add(mogCode);
                    _item.Clear();
                }
                else if (c == '«')
                {
                    var parts = GetEnclosedItem('«', '»');
                    var mogFunction = new MOGFunction(_engine, parts[0]);
                    
                    if (parts.Length > 1)
                        mogFunction.DebugInfo = new DebugInformations(parts[1]);
                    
                    _items.Add(mogFunction);
                    _item.Clear();
                }
                else
                {
                    _item.Append(c);
                }
            }

            if (_item.Length > 0)
            {
                var parsedItem = ParseItem(_item.ToString());
                _items.Add(parsedItem);
                _item.Clear();
            }

            stopWatch.Stop();   

            if (withProgressInformations)
                _engine.Delegate?.DebugMessage(_engine, $"Parsing completed in {stopWatch.ElapsedMilliseconds} ms.");

            return _items;
        }

        private MOGObject ParseItem(string item)
        {
            DebugInformations debugInfo = null;
            var parts = item.Split('§');

            if (parts.Length > 1)
            {
                item = parts[0];
                debugInfo = new DebugInformations(parts[1]);
            }

            if (item == "true")
            {
                var obj = new MOGBoolean(_engine, true);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            if (item == "false")
            {
                var obj = new MOGBoolean(_engine, false);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            if (item == "null")
            {
                var obj = new MOGNull(_engine);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            if (item.StartsWith("0x"))
            {
                if (item.Length > 2)
                {
                    try
                    {
                        float n = System.Convert.ToByte(item.Substring(2), 16);
                        var obj = new MOGNumber(_engine, n);
                        obj.DebugInfo = debugInfo;
                        return obj;
                    }
                    catch
                    {
                        throw new MogwaiParseErrorException("invalid conversion operation");
                    }
                }
                else
                {
                    throw new MogwaiParseErrorException("invalid conversion operation");
                }
            }

            if (item.StartsWith("D:"))
            {
                var content = item.Substring(2);
                MOGData data = null;

                if (content.Length == 0)
                {
                    data = new MOGData(_engine);
                }
                else
                {
                    data = new MOGData(_engine, content);
                }

                data.DebugInfo = debugInfo;

                return data;
            }

            if (item.EndsWith(":") && item.Length > 1)
            {
                var name = item.Substring(0, item.Length - 1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid key {item}");

                var obj = new MOGKey(_engine, name);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            if (item.StartsWith(".") && item.Length > 1)
            {
                var name = item.Substring(1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid type {item}");

                var t = _engine.GetType(name);

                if (t != null)
                {
                    var obj = t.Clone();
                    obj.DebugInfo = debugInfo;
                    return obj;
                }

                throw new MogwaiParseErrorException($"unknown type {item}");
            }

            if (item.StartsWith("&") && item.Length > 1)
            {
                var name = item.Substring(1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid reference {item}");

                var obj = new MOGRef(_engine, name);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            if (LooksLikeNumber(item))
            {
                if (float.TryParse(item, out float value))
                {
                    var obj = new MOGNumber(_engine, value);
                    obj.DebugInfo = debugInfo;  
                    return obj;
                }
            }

            if (_engine.IsPrimitive(item))
            {
                var obj = new MOGPrimitive(_engine, item);
                obj.DebugInfo = debugInfo;
                return obj;
            }

            var objWord = new MOGWord(_engine, item);
            objWord.DebugInfo = debugInfo;
            return objWord  ;
        }

        private static bool LooksLikeNumber(string token)
        {
            int length = token.Length;

            if (length == 0)
                return false;

            char first = token[0];

            if (first >= '0' && first <= '9')
                return true;

            if (first == '+' || first == '-')
            {
                if (length < 2)
                    return false;

                char second = token[1];
                return second >= '0' && second <= '9';
            }

            return false;
        }

        private string[] GetEnclosedItem(char firstChar, char lastChar)
        {
            int level = 0;
            char currentChar = '\0';
            bool inString = false;

            while (_index < _code.Length)
            {
                currentChar = _code[_index++];

                if (currentChar == '"' && firstChar != '"' && lastChar != '"')
                {
                    inString = !inString;
                }
                else
                {
                    if (!inString)
                    {
                        if (currentChar == lastChar)
                        {
                            if (_index > 1 && _code[_index - 2] == '\\')
                            {
                                // escape char, no level up
                            }
                            else if (level == 0 || --level < 0)
                            {
                                if (_index < _code.Length)
                                {
                                    var nextChar = _code[_index];

                                    if (nextChar == '§')
                                    {
                                        _index++;

                                        _item.Append('\x1E');

                                        while (_index < _code.Length)
                                        {
                                            currentChar = _code[_index++];

                                            if (currentChar == ' ')
                                            {
                                                break;
                                            }
                                            else
                                            {
                                                _item.Append(currentChar);
                                            }
                                        }

                                        return _item.ToString().Split('\x1E');
                                    }
                                }

                                return new string[] { _item.ToString() };
                            }
                        }
                        else if (currentChar == firstChar)
                        {
                            if (_index > 1 && _code[_index - 2] == '\\')
                            {
                                // escape char, no level up
                            }
                            else
                            {
                                level++;
                            }
                        }
                    }
                }

                _item.Append(currentChar);
            }

            if (currentChar != lastChar)
                throw new System.Exception($"missing closing character '{lastChar}'");

            return _item.ToString().Split('\x1E');
        }
    }
}
