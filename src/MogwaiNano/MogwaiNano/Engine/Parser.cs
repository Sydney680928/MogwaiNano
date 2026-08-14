using MogwaiNano.Exceptions;
using MogwaiNano.Objects;
using System.Collections;
using System.Text;

namespace MogwaiNano.Engine
{
    public class Parser
    {
        private string _code;
        private ArrayList _items = new();
        private int _index = 0;
        private MogwaiNanoEngine _engine;
        StringBuilder _item = new();

        public Parser(MogwaiNanoEngine engine)
        {
            _engine = engine;
        }

        public ArrayList Parse(string code)
        {
            _code = code;

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
                    GetEnclosedItem('"', '"');
                    _items.Add(new MOGString(_engine, _item.ToString()));
                    _item.Clear();
                }
                else if (c == '\'')
                {
                    GetEnclosedItem('\'', '\'');
                    _items.Add(new MOGName(_engine, _item.ToString()));
                    _item.Clear();
                }
                else if (c == '(')
                {
                    GetEnclosedItem('(', ')');
                    _items.Add(new MOGList(_engine, _item.ToString()));
                    _item.Clear();
                }
                else if (c == '[')
                {
                    GetEnclosedItem('[', ']');
                    _items.Add(new MOGRecord(_engine, _item.ToString()));
                    _item.Clear();
                }
                else if (c == '{')
                {
                    GetEnclosedItem('{', '}');
                    _items.Add(new MOGCode(_engine, _item.ToString()));
                    _item.Clear();
                }
                else if (c == '«')
                {
                    GetEnclosedItem('«', '»');
                    _items.Add(new MOGFunction(_engine, _item.ToString()));
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

            return _items;
        }

        private MOGObject ParseItem(string item)
        {
            if (LooksLikeNumber(item))
            {
                if (float.TryParse(item, out float value))
                    return new MOGNumber(_engine, value);
            }

            if (item == "true")
                return new MOGBoolean(_engine, true);

            if (item == "false")
                return new MOGBoolean(_engine, false);

            if (item == "null")
                return new MOGNull(_engine);


            if (item.EndsWith(":") && item.Length > 1)
            {
                var name = item.Substring(0, item.Length - 1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid key {item}");

                return new MOGKey(_engine, name);
            }

            if (item.StartsWith(".") && item.Length > 1)
            {
                var name = item.Substring(1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid type {item}");

                var t = _engine.GetType(name);

                if (t != null)
                    return t.Clone();

                throw new MogwaiParseErrorException($"unknown type {item}");
            }

            if (item.StartsWith("&") && item.Length > 1)
            {
                var name = item.Substring(1);

                if (!_engine.IsValidName(name, false))
                    throw new MogwaiParseErrorException($"invalid reference {item}");

                return new MOGRef(_engine, name);
            }

            if (item.StartsWith("D:"))
            {
                var content = item.Substring(2);

                if (content.Length == 0)
                {
                    var data = new MOGData(_engine);
                    return data;
                }
                else
                {
                    var data = new MOGData(_engine, content);
                    return data;
                }
            }

            if (_engine.IsPrimitive(item))
                return new MOGPrimitive(_engine, item);

            return new MOGWord(_engine, item);
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

        private void GetEnclosedItem(char firstChar, char lastChar)
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
                                // Caractère d'échappement, on n'augmente pas le niveau
                            }
                            else if (level == 0 || --level < 0)
                            {
                                return;
                            }
                        }
                        else if (currentChar == firstChar)
                        {
                            if (_index > 1 && _code[_index - 2] == '\\')
                            {
                                // Caractère d'échappement, on n'augmente pas le niveau
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
            {
                throw new System.Exception($"missing closing character '{lastChar}'");
            }
        }
    }
}
