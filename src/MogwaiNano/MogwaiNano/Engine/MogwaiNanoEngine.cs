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

using Iot.Device.Ssd13xx;
using Iot.Device.Ssd13xx.Samples;
using MogwaiNano.Interfaces;
using MogwaiNano.Objects;
using nanoFramework.Runtime.Native;
using System;
using System.Collections;
using System.Device.Adc;
using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Pwm;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using static Iot.Device.Ssd13xx.Ssd13xx;
using GC = nanoFramework.Runtime.Native.GC;

namespace MogwaiNano.Engine
{
    public class MogwaiNanoEngine
    {
        private const int IDLE_EVERY_N_ITERATIONS = 5;
        
        private static readonly char[] _invalidChars = { ' ', '\'', '!', '{', '}', '«', '»', '(', ')', '[', ']', '"', ':', '\r', '\n', '\t' };

        private delegate EvalResult PrimitiveDelegate(string name);

        private Hashtable _primitives = new();
        private ArrayList _stacks = new();
        private MOGStack _currentStack = new();
        private Hashtable _timers = new();
        private Hashtable _events = new();
        private Hashtable _types = new(12);
        private ArrayList _varsContext = new();
        private Queue _fireObjectsQueue = new();
        private object _fireObjectsQueueLock = new();
        private object _fireEventLock = new();
        private bool _disableInterrupts;
        private VarContext _currentLocalVarsContext;
        private Hashtable _functions = new(3);
        private AutoResetEvent _runSignal = new(false);
        private string _pendingRunCode;
        private bool _pendingDebugMode;
        private Thread _runThread;
        private Hashtable _openPins = new(3);
        private GpioController _gpioController = new();
        private Hashtable _i2cDevices = new(2);
        private string[] _skills = { "GPIO", "I2C", "SSD1306", "PWM", "ADC" };
        private ArrayList _flags = new();
        private EvalResult _lastResult;
        private Error _lastError;
        private int _iterationCount = 0;
        private object _lastResultLock = new(); 
        private Ssd1306 _ssd1306;
        private Hashtable _pwmChannels = new(2);
        private Hashtable _adcChannels = new(2);
        private AdcController _adcController;

        public readonly MOGType TypeNumber;
        public readonly MOGType TypeString;
        public readonly MOGType TypeBoolean;
        public readonly MOGType TypeName;
        public readonly MOGType TypeList;
        public readonly MOGType TypeRecord;
        public readonly MOGType TypeData;
        public readonly MOGType TypeKey;
        public readonly MOGType TypeCode;
        public readonly MOGType TypeFunction;
        public readonly MOGType TypePrimitive;
        public readonly MOGType TypeType;
        public readonly MOGType TypeWord;
        public readonly MOGType TypeNull;
        public readonly MOGType TypeReference;
        public readonly MOGType TypeAny;

        public Error LastError
        {
            get
            {
                if (_lastError == null)
                    _lastError = Error.None;

                return _lastError;
            }

            set { _lastError = value; }
        }

        public MOGObject CurrentEvalObject { get; set; }

        public IDelegate Delegate { get; set; }

        public bool IsRunning { get; private set; }

        public bool BreakRequested { get; private set; }

        public bool HaltRequested { get; private set; }

        public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

        public EvalResult LastResult
        {
            get
            {
                lock (_lastResultLock)
                {
                    if (_lastResult == null)
                        _lastResult = EvalResult.NoError;

                    return _lastResult;
                }
            }

            private set 
            { 
                lock(_lastResultLock)
                    _lastResult = value;
            }
        }

        public string[] Skills => _skills;

        public bool FrugalMode { get; set; } = false;

        public MogwaiNanoEngine()
        {
            // load types

            TypeNumber = new MOGType(this, "number");
            TypeString = new MOGType(this, "string");
            TypeBoolean = new MOGType(this, "boolean");
            TypeName = new MOGType(this, "name");
            TypeList = new MOGType(this, "list");
            TypeRecord = new MOGType(this, "record");
            TypeData = new MOGType(this, "data");
            TypeKey = new MOGType(this, "key");
            TypeCode = new MOGType(this, "code");
            TypeFunction = new MOGType(this, "function");
            TypePrimitive = new MOGType(this, "primitive");
            TypeType = new MOGType(this, "type");
            TypeWord = new MOGType(this, "word");
            TypeNull = new MOGType(this, "null");
            TypeReference = new MOGType(this, "ref");
            TypeAny = new MOGType(this, "any");

            _types.Add("number", TypeNumber);
            _types.Add("string", TypeString);
            _types.Add("boolean", TypeBoolean);
            _types.Add("name", TypeName);
            _types.Add("list", TypeList);
            _types.Add("record", TypeRecord);
            _types.Add("data", TypeData);
            _types.Add("key", TypeKey);
            _types.Add("code", TypeCode);
            _types.Add("function", TypeFunction);
            _types.Add("primitive", TypePrimitive);
            _types.Add("type", TypeType);
            _types.Add("word", TypeWord);
            _types.Add("ref", TypeReference);
            _types.Add("any", TypeAny);

            // load primitives

            RegisterPrimitives();

            // Create vars context
            // Context zéro = Global vars

            _varsContext.Add(new VarContext("GLOBAL"));

            // Create and start running thread

            _runThread = new Thread(RunLoop);
            _runThread.Start();
        }

        public bool IsPrimitive(string name) => _primitives.Contains(name);

        public MOGType GetType(string name)
        {
            if (_types.Contains(name))
                return _types[name] as MOGType;

            return null;
        }

        public EvalResult ExecutePrimitive(string name)
        {
            var p = _primitives[name] as PrimitiveDelegate;

            if (p == null)
                return EvalResult.Failure(this, Error.UnknownWordError, name);

            return p(name);
        }

        private void RegisterPrimitives()
        {
            _primitives.Add("->type", new PrimitiveDelegate(PrimitiveGetType));

            _primitives.Add("+", new PrimitiveDelegate(PrimitivePlus));
            _primitives.Add("-", new PrimitiveDelegate(PrimitiveMathSubstraction));
            _primitives.Add("*", new PrimitiveDelegate(PrimitiveMathMultiplication));
            _primitives.Add("/", new PrimitiveDelegate(PrimitiveMathDivision));
            _primitives.Add("floor", new PrimitiveDelegate(PrimitiveMathFloor));
            _primitives.Add("mod", new PrimitiveDelegate(PrimitiveMathModulo));

            _primitives.Add("->data", new PrimitiveDelegate(PrimitiveToData));
            _primitives.Add("->bcd", new PrimitiveDelegate(PrimitiveDecimalToBcd));
            _primitives.Add("bcd->", new PrimitiveDelegate(PrimitiveBcdToDecimal));

            _primitives.Add("->vars", new PrimitiveDelegate(PrimitiveStackToVars));
            _primitives.Add("->safeVars", new PrimitiveDelegate(PrimitiveStackToSafeVars));
            _primitives.Add("->params", new PrimitiveDelegate(PrimitiveStackToParams));

            _primitives.Add("makeData", new PrimitiveDelegate(PrimitiveMakeData));

            _primitives.Add("clear", new PrimitiveDelegate(PrimitiveStackClear));
            _primitives.Add("swap", new PrimitiveDelegate(PrimitiveStackSwap));
            _primitives.Add("dup", new PrimitiveDelegate(PrimitiveStackDup));
            _primitives.Add("drop", new PrimitiveDelegate(PrimitiveStackDrop));

            _primitives.Add("break", new PrimitiveDelegate(PrimitiveBreak));

            _primitives.Add("wait", new PrimitiveDelegate(PrimitiveWait));
            _primitives.Add("get", new PrimitiveDelegate(PrimitiveGet));
            _primitives.Add("set", new PrimitiveDelegate(PrimitiveSet));
            _primitives.Add("size", new PrimitiveDelegate(PrimitiveSize));

            _primitives.Add("DI", new PrimitiveDelegate(PrimitiveDI));
            _primitives.Add("EI", new PrimitiveDelegate(PrimitiveEI));

            _primitives.Add("==", new PrimitiveDelegate(PrimitiveConditionEqual));
            _primitives.Add("!=", new PrimitiveDelegate(PrimitiveConditionNotEqual));
            _primitives.Add("<", new PrimitiveDelegate(PrimitiveConditionInferior));
            _primitives.Add(">", new PrimitiveDelegate(PrimitiveConditionSuperior));
            _primitives.Add("<=", new PrimitiveDelegate(PrimitiveConditionInferiorOrEqual));
            _primitives.Add(">=", new PrimitiveDelegate(PrimitiveConditionSuperiorOrEqual));
            _primitives.Add("not", new PrimitiveDelegate(PrimitiveNot));
            _primitives.Add("isnull", new PrimitiveDelegate(PrimitiveConditionIsNull));
            _primitives.Add("and", new PrimitiveDelegate(PrimitiveConditionAnd));
            _primitives.Add("or", new PrimitiveDelegate(PrimitiveConditionOr));
            _primitives.Add("xor", new PrimitiveDelegate(PrimitiveConditionXor));

            _primitives.Add("&", new PrimitiveDelegate(PrimitiveBinaryAnd));
            _primitives.Add("|", new PrimitiveDelegate(PrimitiveBinaryOr));
            _primitives.Add("^", new PrimitiveDelegate(PrimitiveBinaryXor));
            _primitives.Add("~", new PrimitiveDelegate(PrimitiveBinaryComplement));
            _primitives.Add("<<", new PrimitiveDelegate(PrimitiveLeftShift));
            _primitives.Add(">>", new PrimitiveDelegate(PrimitiveRightShift));

            _primitives.Add("console.println", new PrimitiveDelegate(PrimitiveConsolePrintLn));
            _primitives.Add("?", new PrimitiveDelegate(PrimitiveConsolePrintLn));
            _primitives.Add("console.print", new PrimitiveDelegate(PrimitiveConsolePrint));
            _primitives.Add("??", new PrimitiveDelegate(PrimitiveConsolePrint));

            _primitives.Add("->format", new PrimitiveDelegate(PrimitiveToFormat));
            _primitives.Add("sub", new PrimitiveDelegate(PrimitiveSub));
            _primitives.Add("->num", new PrimitiveDelegate(PrimitiveToNumber));

            _primitives.Add("EVENT", new PrimitiveDelegate(PrimitiveEvent));
            _primitives.Add("event.fire", new PrimitiveDelegate(PrimitiveEventFire));
            _primitives.Add("event.purge", new PrimitiveDelegate(PrimitiveEventPurge));

            _primitives.Add("AFTER", new PrimitiveDelegate(PrimitiveTimerAfter));
            _primitives.Add("EVERY", new PrimitiveDelegate(PrimitiveTimerEvery));
            _primitives.Add("timer.start", new PrimitiveDelegate(PrimitiveTimerStart));
            _primitives.Add("timer.stop", new PrimitiveDelegate(PrimitiveTimerStop));
            _primitives.Add("timer.purge", new PrimitiveDelegate(PrimitiveTimerPurge));

            _primitives.Add("skills", new PrimitiveDelegate(PrimitiveGetSkills));
            _primitives.Add("hasSkill", new PrimitiveDelegate(PrimitiveHasSkill));

            _primitives.Add("flag.set", new PrimitiveDelegate(PrimitiveFlagSet));
            _primitives.Add("flag.clear", new PrimitiveDelegate(PrimitiveFlagClear));
            _primitives.Add("flag.isSet", new PrimitiveDelegate(PrimitiveFlagIsSet));
            _primitives.Add("flag.isClear", new PrimitiveDelegate(PrimitiveFlagIsClear));

            _primitives.Add("debug.write", new PrimitiveDelegate(PrimitiveDebugWrite));

            _primitives.Add("mogwai.halt", new PrimitiveDelegate(PrimitiveHalt));
            _primitives.Add("mogwai.memory", new PrimitiveDelegate(PrimitiveGetMemory));
            _primitives.Add("mogwai.reset", new PrimitiveDelegate(PrimitiveMogwaiReset));
            _primitives.Add("mogwai.sendMessage", new PrimitiveDelegate(PrimitiveSendMessageToStudio));
            _primitives.Add("mogwai.reboot", new PrimitiveDelegate(PrimitiveMogwaiReboot));
            _primitives.Add("mogwai.info", new PrimitiveDelegate(PrimitiveMogwaiInfo));
            _primitives.Add("mogwai.frugalMode", new PrimitiveDelegate(PrimitiveMogwaiFrugalMode));

            _primitives.Add("gpio.setMode.input", new PrimitiveDelegate(PrimitiveGpioModeInput));
            _primitives.Add("gpio.setMode.inputPullDown", new PrimitiveDelegate(PrimitiveGpioSetModeInputPullDown));
            _primitives.Add("gpio.setMode.inputPullUp", new PrimitiveDelegate(PrimitiveGpioSetModeInputPullUp));
            _primitives.Add("gpio.setMode.output", new PrimitiveDelegate(PrimitiveGpioSetModeOutput));
            _primitives.Add("gpio.read", new PrimitiveDelegate(PrimitiveGpioPinRead));
            _primitives.Add("gpio.write.high", new PrimitiveDelegate(PrimitiveGpioPinWriteHigh));
            _primitives.Add("gpio.write.low", new PrimitiveDelegate(PrimitiveGpioPinWriteLow));
            _primitives.Add("gpio.toggle", new PrimitiveDelegate(PrimitiveGpioPinToggle));
            _primitives.Add("gpio.close", new PrimitiveDelegate(PrimitiveGpioPinClose));

            _primitives.Add("i2c.open", new PrimitiveDelegate(PrimitiveI2cOpen));
            _primitives.Add("i2c.close", new PrimitiveDelegate(PrimitiveI2cClose));
            _primitives.Add("i2c.write", new PrimitiveDelegate(PrimitiveI2cWrite));
            _primitives.Add("i2c.register.write", new PrimitiveDelegate(PrimitiveI2cRegisterWrite));
            _primitives.Add("i2c.read", new PrimitiveDelegate(PrimitiveI2cRead));
            _primitives.Add("i2c.register.read", new PrimitiveDelegate(PrimitiveI2cRegisterRead));
            _primitives.Add("i2c.scan", new PrimitiveDelegate(PrimitiveI2cScan));

            _primitives.Add("ssd1306.init", new PrimitiveDelegate(PrimitiveSsd1306Init));
            _primitives.Add("ssd1306.close", new PrimitiveDelegate(PrimitiveSsd1306Close));
            _primitives.Add("ssd1306.clear", new PrimitiveDelegate(PrimitiveSsd1306Clear));  
            _primitives.Add("ssd1306.printString", new PrimitiveDelegate(PrimitiveSsd1306PrintString));
            _primitives.Add("ssd1306.drawString", new PrimitiveDelegate(PrimitiveSsd1306DrawString));
            _primitives.Add("ssd1306.refresh", new PrimitiveDelegate(PrimitiveSsd1306Refresh));
            _primitives.Add("ssd1306.drawPixel", new PrimitiveDelegate(PrimitiveSsd1306DrawPixel));
            _primitives.Add("ssd1306.drawHorizontalLine", new PrimitiveDelegate(PrimitiveSsd1306DrawHorizontalLine));
            _primitives.Add("ssd1306.drawVerticalLine", new PrimitiveDelegate(PrimitiveSsd1306DrawHVerticalLine));
            _primitives.Add("ssd1306.drawRectangle", new PrimitiveDelegate(PrimitiveSsd1306DrawRectangle));
            _primitives.Add("ssd1306.drawFilledRectangle", new PrimitiveDelegate(PrimitiveSsd1306DrawFilledRectangle));
            _primitives.Add("ssd1306.drawBitmap", new PrimitiveDelegate(PrimitiveSsd1306DrawBitmap));

            _primitives.Add("pwm.open", new PrimitiveDelegate(PrimitivePwmOpen));
            _primitives.Add("pwm.close", new PrimitiveDelegate(PrimitivePwmClose));
            _primitives.Add("pwm.start", new PrimitiveDelegate(PrimitivePwmStart));
            _primitives.Add("pwm.stop", new PrimitiveDelegate(PrimitivePwmStop));

            _primitives.Add("adc.open", new PrimitiveDelegate(PrimitiveAdcOpen));   
            _primitives.Add("adc.close", new PrimitiveDelegate(PrimitiveAdcClose));
            _primitives.Add("adc.read", new PrimitiveDelegate(PrimitiveAdcReadValue));
            _primitives.Add("adc.resolutionInBits", new PrimitiveDelegate(PrimitiveAdcGetResolutionInBits));
            _primitives.Add("adc.maxValue", new PrimitiveDelegate(PrimitiveAdcGetMaxValue));    

            _primitives.Add("device.setPinFunction", new PrimitiveDelegate(PrimitiveDeviceSetPinFunction));

            _primitives.Add("STO", new PrimitiveDelegate(PrimitiveSto));
            _primitives.Add("REPEAT", new PrimitiveDelegate(PrimitiveRepeat));
            _primitives.Add("IF", new PrimitiveDelegate(PrimitiveIf));
            _primitives.Add("IFELSE", new PrimitiveDelegate(PrimitiveIfElse));
            _primitives.Add("WHILE", new PrimitiveDelegate(PrimitiveWhile));
            _primitives.Add("FOR", new PrimitiveDelegate(PrimitiveFor));
            _primitives.Add("FORSTEP", new PrimitiveDelegate(PrimitiveForStep));
            _primitives.Add("FOREVER", new PrimitiveDelegate(PrimitiveForever));
            _primitives.Add("DEFUNC", new PrimitiveDelegate(PrimitiveDefunc));
            _primitives.Add("FOREACH", new PrimitiveDelegate(PrimitiveForeach));
        }

        private void RunLoop()
        {
            while (true)
            {
                _runSignal.WaitOne();

                if (!string.IsNullOrEmpty(_pendingRunCode))
                {
                    var code = _pendingRunCode;
                    var debugMode = _pendingDebugMode;

                    _pendingRunCode = null;

                    var executionThread = new Thread(() => Run(code, debugMode));
                    executionThread.Start();
                }
            }
        }

        public void Idle()
        {
            _iterationCount++;

            if (_iterationCount % IDLE_EVERY_N_ITERATIONS == 0)
            {
                _iterationCount = 0;
                Thread.Sleep(1);
            }
        }

        public bool IsValidName(string name, bool withPrimitiveChecking)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var c1 = name[0];
            var c2 = name.Length > 1 ? name[1] : '\0';

            if (withPrimitiveChecking)
            {
                if (_primitives.Contains(name))
                    return false;
            }

            return name.IndexOfAny(_invalidChars) == -1;
        }

        public EvalResult Run(string code, bool debugMode = false)
        {
            Debug.WriteLine($"run: GC: {GC.Run(true)} bytes free");

            try
            {
                IsRunning = true;

                Reset();

                // _debugMode = debugMode

                var stopwatch = Stopwatch.StartNew();

                if (Delegate != null)
                    Delegate.ProgramStart(this, code);

                MOGFunction program;

                try
                {
                    program = new MOGFunction(this, code);
                    code = null;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    LastResult = EvalResult.ParseFailure(this, ex.Message);

                    if (Delegate != null)
                        Delegate.ProgramEnd(this, LastResult);

                    return LastResult;
                }

                HaltRequested = false;
                //ExitRequested = false;
                //ReturnRequested = false;

                EvalResult result = program.Execute();

                HaltRequested = false;
                //ExitRequested = false;
                //ReturnRequested = false;

                EvalResult result2;

                if (result.IsError)
                {
                    if (_functions.Contains("MOGWAI.onError"))
                    {
                        var onErrorFunction = _functions["MOGWAI.onError"] as MOGFunction;
                        result2 = onErrorFunction.Execute();

                        if (result2.IsError)
                            result = result2;
                    }
                }
                else
                {
                    if (_functions.Contains("MOGWAI.onStop"))
                    {
                        var onStopFunction = _functions["MOGWAI.onStop"] as MOGFunction;
                        result2 = onStopFunction.Execute();

                        if (result2.IsError)
                            result = result2;
                    }
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                // Reset(_keepAlive);

                // _debugMode = false;

                LastResult = result;

                if (Delegate != null)
                    Delegate.ProgramEnd(this, result);
              
                return result;
            }
            finally
            {
                GC.Run(true);
                IsRunning = false;
            }
        }

        public bool RunAsync(string code, bool debugMode = false)
        {
            if (IsRunning)
                return false;

            _pendingRunCode = code;
            _pendingDebugMode = debugMode;

            _runSignal.Set();

            return true;
        }

        public void Reset(bool keepAlive = false)
        {
            ClearTimers();

            ClearEvents();

            ClearWaitingFireObjects();

            CleanupOpenPins();

            CleanupI2cDevices();

            CleanupPwmChannels();

            CleanupAdcChannels();

            if (_ssd1306 != null)
            {
                _ssd1306.Dispose();
                _ssd1306 = null;
            }

            _stacks.Clear();
            _currentStack = new MOGStack();
            _stacks.Add(_currentStack);

            var glb = _varsContext[0] as VarContext;
            glb.Clear();

            _functions.Clear();

            _disableInterrupts = false;

            HaltRequested = false;
            BreakRequested = false;

            FrugalMode = false;
        }

        public void Halt() => HaltRequested = true;

        #region STACK

        public void AddNewStack()
        {
            _currentStack = new MOGStack();
            _stacks.Add(_currentStack);
        }

        public void RemoveLastStack()
        {
            if (_stacks.Count > 1)
            {
                _stacks.RemoveAt(_stacks.Count - 1);
                _currentStack = _stacks[_stacks.Count - 1] as MOGStack;
            }
        }

        public int StackSize => _currentStack.Count;

        public void StackPush(MOGObject item) => _currentStack.Push(item);

        public MOGObject StackPop() => _currentStack.Pop();

        public Type[] StackSign(int count) => _currentStack.Sign(count);

        public void StackClear() => _currentStack.Clear();

        public bool StackSwap() => _currentStack.Swap();

        public void StackDup() => _currentStack.Dup();

        public void StackDrop() => _currentStack.Drop();

        #endregion

        #region PRIMITIVES

        private EvalResult PrimitivePlus(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // 15 20 +

                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                n0.Value += n1.Value;

                StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // "AA" "BB" +

                var s1 = StackPop() as MOGString;
                var s0 = StackPop() as MOGString;

                s0.Value += s1.Value;

                StackPush(s0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGString))
            {
                // "BB" 456 +

                var s1 = StackPop() as MOGNumber;
                var s0 = StackPop() as MOGString;

                s0.Value += s1.Value.ToString();

                StackPush(s0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGNumber))
            {
                // 456 "BB" +

                var s1 = StackPop() as MOGString;
                var s0 = StackPop() as MOGNumber;

                s1.Value = s0.Value.ToString() + s1.Value;

                StackPush(s1);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGList))
            {
                // (1 2 3) "TOTO" + 

                var item = StackPop();

                var list = StackPop() as MOGList;
                list.AddItem(item);

                StackPush(list);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGData) && s[0] == typeof(MOGNumber))
            {
                // D:FFAB45 123 +

                var value = StackPop() as MOGNumber;
                var data = StackPop() as MOGData;

                data.AddItem((byte)value.Value);

                StackPush(data);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, @ref.ToString());

                StackPush(value);
                StackPush(item);

                var r = PrimitivePlus(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMathSubstraction(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                n0.Value -= n1.Value;

                StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, @ref.ToString());

                StackPush(value);
                StackPush(item);

                var r = PrimitiveMathSubstraction(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMathMultiplication(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                n0.Value *= n1.Value;

                StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, @ref.ToString());

                StackPush(value);
                StackPush(item);

                var r = PrimitiveMathMultiplication(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMathDivision(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                n0.Value /= n1.Value;

                StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, @ref.ToString());

                StackPush(value);
                StackPush(item);

                var r = PrimitiveMathDivision(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMathFloor(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var number = StackPop() as MOGNumber;

            try
            {
                var n = new MOGNumber(this, (float)Math.Floor((double)number.Value));
                StackPush(n);

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.MathematicalError, name, ex.Message);
            }
        }

        private EvalResult PrimitiveMathModulo(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var n1 = StackPop() as MOGNumber;

            try
            {
                var v = n1.Value % n0.Value;
                StackPush(new MOGNumber(this, v));

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.MathematicalError, name, ex.Message);
            }
        }

        private EvalResult PrimitiveGetType(string name)
        {
            if (StackSize < 1)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var n0 = StackPop();
            StackPush(n0.Type.Clone());

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackClear(string name)
        {
            StackClear();
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackSwap(string name)
        {
            if (StackSize < 2)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            StackSwap();
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackDup(string name)
        {
            if (StackSize < 1)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            StackDup();
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackDrop(string name)
        {
            if (StackSize == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            StackDrop();
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveDebugWrite(string name)
        {
            if (StackSize == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var n0 = StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name.ToString());

                StackPush(value);

                var r = PrimitiveDebugWrite(name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return Delegate.DebugMessage(this, @string.Value);
                }
                else
                {
                    return Delegate.DebugMessage(this, n0.ToString());
                }
            }
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveConsolePrintLn(string name)
        {
            if (StackSize == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var n0 = StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name.ToString());

                StackPush(value);

                var r = PrimitiveConsolePrintLn(name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return Delegate.ConsolePrintLn(this, @string.Value);
                }
                else
                {
                    return Delegate.ConsolePrintLn(this, n0.ToString());
                }
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveConsolePrint(string name)
        {
            if (StackSize == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var n0 = StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name.ToString());

                StackPush(value);

                var r = PrimitiveConsolePrint(name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return Delegate.ConsolePrint(this, @string.Value);
                }
                else
                {
                    return Delegate.ConsolePrint(this, n0.ToString());
                }
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveSto(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n = StackPop() as MOGName;
            var value = StackPop() as MOGObject;

            if (!IsValidName(n.Value, true))
                return EvalResult.Failure(this, Error.InvalidNameError, name, n.ToString());

            return VarWrite(n.Value, value);
        }

        private EvalResult PrimitiveBreak(string name)
        {
            BreakRequested = true;
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveHalt(string name)
        {
            HaltRequested = true;
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveWait(string name)
        {
            // 50 wait

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var v = StackPop() as MOGNumber;

            if (v.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalMilliseconds <= v.Value)
            {
                Thread.Sleep(0);

                var result = ExecuteWaitingFireObjects();

                if (result != EvalResult.NoError)
                    return result;
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveGet(string name)
        {
            // (1 2 3) 0 get
            // [x: 50 y: 10] x: get

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGList))
            {
                // (1 2 3) 0 get

                var index = StackPop() as MOGNumber;
                var list = StackPop() as MOGList;

                if (index.Value < 0 || index.Value >= list.Items.Count)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                var item = list.GetItem((int)index.Value);
                StackPush(item);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGKey) && s[1] == typeof(MOGRecord))
            {
                // [x: 10 y: 20] x: get

                var key = StackPop() as MOGKey;
                var record = StackPop() as MOGRecord;

                var item = record.GetItem(key.Value);

                if (item == null)
                {
                    StackPush(new MOGNull(this));
                }
                else
                {
                    StackPush(item);
                }

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData))
            {
                // D:FFAA45 0 get

                var index = StackPop() as MOGNumber;
                var data = StackPop() as MOGData;

                if (index.Value < 0 || index.Value >= data.Items.Length)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                var item = data.GetItem((int)index.Value);
                StackPush(new MOGNumber(this, item));

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X get

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, @ref.Value);

                StackPush(value);
                StackPush(item);

                var r = PrimitiveGet(name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSet(string name)
        {
            // 10 (1 2 3) 0 set ---> (10 2 3)
            // 100 [x: 10 y: 20] x: set ---> [x: 100 y: 20]

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGList))
            {
                // 10 (1 2 3) 0 set

                var index = StackPop() as MOGNumber;
                var list = StackPop() as MOGList;
                var value = StackPop();

                if (index.Value < 0 || index.Value >= list.Items.Count)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                list.SetItem((int)index.Value, value);
                StackPush(list);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGKey) && s[1] == typeof(MOGRecord))
            {
                // 100 [x: 10 y: 20] x: set

                var key = StackPop() as MOGKey;
                var record = StackPop() as MOGRecord;
                var value = StackPop();

                record.SetItem(key.Value, value);
                StackPush(record);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData) && s[2] == typeof(MOGNumber))
            {
                // 10 D:FFAA45 0 set

                var index = StackPop() as MOGNumber;
                var data = StackPop() as MOGData;
                var value = StackPop() as MOGNumber;

                if (index.Value < 0 || index.Value >= data.Items.Length)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                data.SetItem((int)index.Value, (byte)value.Value);
                StackPush(data);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // 10 &X 5 set
                // 10 &X x: set

                var item = StackPop();
                var @ref = StackPop() as MOGRef;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name);

                StackPush(value);
                StackPush(item);

                var r = PrimitiveSet(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSize(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGList))
            {
                var list = StackPop() as MOGList;
                StackPush(new MOGNumber(this, list.Items.Count));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRecord))
            {
                var record = StackPop() as MOGRecord;
                StackPush(new MOGNumber(this, record.Items.Count));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString))
            {
                var @string = StackPop() as MOGString;
                StackPush(new MOGNumber(this, @string.Value.Length));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGData))
            {
                var data = StackPop() as MOGData;
                StackPush(new MOGNumber(this, data.Items.Length));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRef))
            {
                var @ref = StackPop() as MOGRef;
                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name.ToString());

                StackPush(value);

                var r = PrimitiveSize(name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveDI(string name)
        {
            _disableInterrupts = true;
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveEI(string name)
        {
            _disableInterrupts = false;
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveToData(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var n0 = StackPop() as MOGNumber;
                var size = (int)n0.Value;

                if (size > StackSize)
                    return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

                var types = StackSign(size);

                for (int i = 0; i < size; i++)
                {
                    if (types[i] != typeof(MOGNumber))
                        return EvalResult.Failure(this, Error.BadArgumentValueError, name, "only numbers between 0 and 255 are allowed.");
                }

                var items = new byte[size];

                for (int i = size - 1; i >= 0; i--)
                {
                    var n = StackPop() as MOGNumber;
                    items[i] = (byte)n.Value;
                }

                var data = new MOGData(this, items);

                StackPush(data);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMakeData(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var initValue = StackPop() as MOGNumber;
                var size = StackPop() as MOGNumber;

                var items = new byte[(int)size.Value];
                var value = (byte)initValue.Value;

                if (value != 0)
                {
                    for (int i = 0; i < items.Length; i++)
                        items[i] = value;
                }

                var data = new MOGData(this, items);

                StackPush(data);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionEqual(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // n1 n2 ==

                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value == n1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGType) && s[1] == typeof(MOGType))
            {
                // t1 t2 ==

                var t1 = StackPop() as MOGType;
                var t0 = StackPop() as MOGType;

                StackPush(new MOGBoolean(this, t0.Value == t1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // s1 s2 ==

                var s1 = StackPop() as MOGString;
                var s0 = StackPop() as MOGString;

                StackPush(new MOGBoolean(this, s0.Value == s1.Value));

                return EvalResult.NoError;
            }   

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionNotEqual(string name)
        {
            // v1 v2 !=

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // n1 n2 != 

                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value != n1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGType) && s[1] == typeof(MOGType))
            {
                // t1 t2 !=

                var t1 = StackPop() as MOGType;
                var t0 = StackPop() as MOGType;

                StackPush(new MOGBoolean(this, t0.Value != t1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // s1 s2 !=

                var t1 = StackPop() as MOGString;
                var t0 = StackPop() as MOGString;

                StackPush(new MOGBoolean(this, t0.Value != t1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionInferior(string name)
        {
            // v1 v2 <

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value < n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionInferiorOrEqual(string name)
        {
            // v1 v2 <=

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value <= n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionSuperior(string name)
        {
            // v1 v2 >

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value > n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionSuperiorOrEqual(string name)
        {
            // v1 v2 >=

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = StackPop() as MOGNumber;
                var n0 = StackPop() as MOGNumber;

                StackPush(new MOGBoolean(this, n0.Value >= n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionIsNull(string name)
        {
            if (StackSize == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var n0 = StackPop();
            var b = n0 is MOGNull;

            StackPush(new MOGBoolean(this, b));
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveConditionAnd(string name)
        {
            // true false and

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = StackPop() as MOGBoolean;
                var n0 = StackPop() as MOGBoolean;

                StackPush(new MOGBoolean(this, n0.Value && n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionOr(string name)
        {
            // true false or

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = StackPop() as MOGBoolean;
                var n0 = StackPop() as MOGBoolean;

                StackPush(new MOGBoolean(this, n0.Value || n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveConditionXor(string name)
        {
            // true false xor

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = StackPop() as MOGBoolean;
                var n0 = StackPop() as MOGBoolean;

                StackPush(new MOGBoolean(this, n0.Value ^ n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveNot(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGBoolean))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var b = StackPop() as MOGBoolean;
            b.Value = !b.Value;
            StackPush(b);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveRepeat(string name)
        {
            // 5 {...} REPEAT

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[1] != typeof(MOGNumber) || s[0] != typeof(MOGCode))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var code = StackPop() as MOGCode;
            var n = StackPop() as MOGNumber;

            for (int i = 0; i < n.Value; i++)
            {
                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (BreakRequested)
                {
                    BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveIf(string name)
        {
            try
            {
                // true {...} IF

                var s = StackSign(2);

                if (s.Length == 0)
                    return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

                if (s[1] != typeof(MOGBoolean) || s[0] != typeof(MOGCode))
                    return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

                var code = StackPop() as MOGCode;
                var condition = StackPop() as MOGBoolean;

                if (condition.Value)
                    return code.Execute();

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.FatalError, name, ex.Message);
            }
        }

        private EvalResult PrimitiveIfElse(string name)
        {
            try
            {
                // true {...} {...} IFELSE

                var s = StackSign(3);

                if (s.Length == 0)
                    return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

                if (s[2] != typeof(MOGBoolean) || s[1] != typeof(MOGCode) || s[0] != typeof(MOGCode))
                    return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

                var code1 = StackPop() as MOGCode;
                var code2 = StackPop() as MOGCode;
                var condition = StackPop() as MOGBoolean;

                if (condition.Value)
                    return code2.Execute();

                return code1.Execute();
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.FatalError, name, ex.Message);
            }
        }

        private EvalResult PrimitiveWhile(string name)
        {
            // { condition } { code } WHILE

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGCode) || s[1] != typeof(MOGCode))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var code = StackPop() as MOGCode;
            var conditionCode = StackPop() as MOGCode;

            while (true)
            {
                var conditionResult = conditionCode.Execute();

                if (conditionResult.IsError)
                    return conditionResult;

                if (BreakRequested)
                {
                    BreakRequested = false;
                    break;
                }

                var conditionValue = StackPop() as MOGBoolean;

                if (conditionValue == null)
                    return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

                if (!conditionValue.Value)
                    break;

                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (BreakRequested)
                {
                    BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveFor(string name)
        {
            // 1 2 'i' {...} FOR

            var s = StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var code = StackPop() as MOGCode;
                var varName = StackPop() as MOGName;
                var end = StackPop() as MOGNumber;
                var start = StackPop() as MOGNumber;

                var direction = (end!.Value - start!.Value) > 0 ? 1 : -1;
                var varLoop = new MOGNumber(this, 0);

                EvalResult result = EvalResult.NoError;

                for (float i = start.Value; direction > 0 ? i <= end.Value : i >= end.Value; i += direction)
                {
                    if (BreakRequested)
                    {
                        BreakRequested = false;
                        break;
                    }

                    varLoop.Value = i;
                    result = VarWrite(varName.Value, varLoop);

                    if (result != EvalResult.NoError)
                        break;

                    result = code.Execute();

                    if (result != EvalResult.NoError)
                        break;
                }

                return result;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveForStep(string name)
        {
            // 1 2 2 'i' {...} FORSTEP

            var s = StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var code = StackPop() as MOGCode;
                var varName = StackPop() as MOGName;
                var step = StackPop() as MOGNumber;
                var end = StackPop() as MOGNumber;
                var start = StackPop() as MOGNumber;

                var direction = (end.Value - start.Value) > 0 ? 1 : -1;
                step.Value = Math.Abs(step.Value) * direction;
                
                var varLoop = new MOGNumber(this, 0);

                EvalResult result = EvalResult.NoError;

                for (float i = start.Value; direction > 0 ? i <= end.Value : i >= end.Value; i += step.Value)
                {
                    if (BreakRequested)
                    {
                        BreakRequested = false;
                        break;
                    }

                    varLoop.Value = i;
                    result = VarWrite(varName.Value, varLoop);

                    if (result != EvalResult.NoError)
                        break;

                    result = code.Execute();

                    if (result != EvalResult.NoError)
                        break;
                }

                return result;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveForever(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGCode))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var code = StackPop() as MOGCode;

            while (true)
            {
                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (BreakRequested)
                {
                    BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveForeach(string name)
        {
            // List name code FOREACH
            // (1 2 3) 'i' { i ? } FOREACH      
            // D:010203 'i' { i ? } FOREACH 
            // "XXXX" 'i' { i ? } FOREACH

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            // 0 code
            // 1 variable
            // 2 list base

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName))
            {
                if (s[2] == typeof(MOGList))
                {
                    var code = StackPop() as MOGCode;
                    var varName = StackPop() as MOGName;
                    var list = StackPop() as MOGList;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in list.Items)
                    {
                        if (BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = VarWrite(varName.Value, item as MOGObject);

                        if (result.IsError)
                            break;

                        result = code.Execute();

                        if (result.IsError)
                            break;
                    }

                    return result;
                }
                else if (s[2] == typeof(MOGData))
                {
                    var code = StackPop() as MOGCode;
                    var varName = StackPop() as MOGName;
                    var data = StackPop() as MOGData;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in data.Items)
                    {
                        if (BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = VarWrite(varName.Value, new MOGNumber(this, (byte)item));

                        if (result != EvalResult.NoError)
                            break;

                        result = code.Execute();

                        if (result != EvalResult.NoError)
                            break;
                    }

                    return result;
                }
                else if (s[2] == typeof(MOGString))
                {
                    var code = StackPop() as MOGCode;
                    var varName = StackPop() as MOGName;
                    var @string = StackPop() as MOGString;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in @string.Value)
                    {
                        if (BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = VarWrite(varName.Value, new MOGString(this, item.ToString()));

                        if (result != EvalResult.NoError)
                            break;

                        result = code.Execute();

                        if (result != EvalResult.NoError)
                            break;
                    }

                    return result;
                }

            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveDefunc(string name)
        {
            // code name DEFUNC

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGFunction))
            {
                var fname = StackPop() as MOGName;
                var func = StackPop() as MOGFunction;

                if (_functions.Contains(fname.Value))
                    return EvalResult.Failure(this, Error.FunctionAlreadyExistsError, name);

                _functions.Add(fname.Value, func);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveEvent(string name)
        {
            // function name EVENT

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) & s[1] == typeof(MOGFunction))
            {
                var eventName = StackPop() as MOGName;
                var function = StackPop() as MOGFunction;

                return CreateNewEvent(eventName.Value, function);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveEventFire(string name)
        {
            // 'BTN_CLICK' data event.fire
            // 'BTN_CLICK' null event.fire
            // 'BTN_CLICK' now event.fire

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[1] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop();
            var n1 = StackPop() as MOGName;

            return FireEvent(n1.Value, n0);
        }

        private EvalResult PrimitiveEventPurge(string name)
        {
            // 'eventName' event.purge

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var eventName = StackPop() as MOGName;
                return PurgeEvent(eventName.Value);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveTimerEvery(string name)
        {
            // function interval name EVERY

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGFunction))
            {
                var timerName = StackPop() as MOGName;
                var interval = StackPop() as MOGNumber;
                var function = StackPop() as MOGFunction;

                return CreateNewTimer(timerName.Value, (int)interval.Value, true, function);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveTimerAfter(string name)
        {
            // function interval name AFTER

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGFunction))
            {
                var timerName = StackPop() as MOGName;
                var interval = StackPop() as MOGNumber;
                var function = StackPop() as MOGFunction;

                return CreateNewTimer(timerName.Value, (int)interval.Value, false, function);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

        }

        private EvalResult PrimitiveTimerStart(string name)
        {
            // name timer.start

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = StackPop() as MOGName;

                if (!_timers.Contains(timerName.Value))
                    return EvalResult.Failure(this, Error.UnknownNameError, name);

                var timer = _timers[timerName.Value] as MOGTimer;
                return timer.Start();
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveTimerStop(string name)
        {
            // name timer.stop

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = StackPop() as MOGName;

                if (!_timers.Contains(timerName.Value))
                    return EvalResult.Failure(this, Error.UnknownNameError, name);

                var timer = _timers[timerName.Value] as MOGTimer;
                return timer.Stop();
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveTimerPurge(string name)
        {
            // name timer.purge

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = StackPop() as MOGName;
                return PurgeTimer(timerName.Value);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveMogwaiReset(string name)
        {
            Reset();
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveMogwaiReboot(string name)
        {
            if (_functions.Contains("MOGWAI.onReboot"))
            {
                var onRebootFunction = _functions["MOGWAI.onReboot"] as MOGFunction;
                var r = onRebootFunction.Execute();

                if (r.IsError)
                    return r;
            }

            Thread.Sleep(1000);

            Power.RebootDevice(5000, RebootOption.ClrOnly);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveGetMemory(string name)
        {
            // true or false getMemory

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var b = StackPop() as MOGBoolean;
            var v = GC.Run(b.Value);

            StackPush(new MOGNumber(this, v));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveMogwaiInfo(string name)
        {
            var record = new MOGRecord(this);

            record.SetItem("name", new MOGString(this, AppGlobal.NanoParameters.Name));
            record.SetItem("mogwai", new MOGString(this, AppGlobal.MogwaiNanoEngine.Version.ToString()));
            record.SetItem("ip", new MOGString(this, value: AppGlobal.IpAddress));
            record.SetItem("session", new MOGString(this, AppGlobal.Session.ToString()));

            record.SetItem("platform", new MOGString(this, SystemInfo.Platform));
            record.SetItem("target", new MOGString(this, SystemInfo.TargetName));
            record.SetItem("oem", new MOGString(this, SystemInfo.OEMString));
            record.SetItem("system", new MOGString(this, SystemInfo.Version.ToString()));

            var memory = GC.Run(false);
            record.SetItem("memory", new MOGNumber(this, memory));

            var skills = new MOGList(this);

            foreach (var skill in _skills)
                skills.AddItem(new MOGString(this, skill));

            record.SetItem("skills", skills);

            record.SetItem("frugalMode", new MOGBoolean(this, FrugalMode)); 

            StackPush(record);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveMogwaiFrugalMode(string name)
        {
            // true or false frugalMode

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGBoolean))
            {
                var b = StackPop() as MOGBoolean;
                FrugalMode = b.Value;
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);  
        }

        private EvalResult PrimitiveSendMessageToStudio(string name)
        {
            //"MESSAGE" mogwai.sendMessage

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString))
            {
                var payload = StackPop() as MOGString;
                var message = new ServerMessage(AppGlobal.NanoParameters.Name, "SEND.MESSAGE", payload.Value);
                AppGlobal.TcpServer.SendMessage(message);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveBcdToDecimal(string name)
        {
            // bcd->

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var number = StackPop() as MOGNumber;

            if (number == null)
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            int bcdValue = (int)number.Value;
            int decimalValue = ((bcdValue >> 4) * 10) + (bcdValue & 0x0F);

            StackPush(new MOGNumber(this, decimalValue));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveDecimalToBcd(string name)
        {
            // ->bcd

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            var number = StackPop() as MOGNumber;

            if (number == null)
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            int decimalValue = (int)number.Value;

            if (decimalValue < 0 || decimalValue > 99)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name); // BCD sur un octet = 0-99

            int bcdValue = ((decimalValue / 10) << 4) | (decimalValue % 10);

            StackPush(new MOGNumber(this, bcdValue));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackToVars(string name)
        {
            // 10 20 30 ( 'A' 'B' 'C') ->vars -----> A=10 B=20 C=30
            // [id: 50 name: "SIBUE" x: 'Z'] ->vars -------> id=50 name="SIBUE" x='Z'

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGList))
            {
                // Signature 10 20 30 ( 'A' 'B' 'C') ->vars

                var list = StackPop() as MOGList;

                // La liste ne doit comporter QUE des names

                foreach (var item in list.Items)
                {
                    if (item is not MOGName)
                        return EvalResult.Failure(this, Error.BadArgumentTypeError, name, "the list parameter can only contain names.");
                }

                // La stack doit comporter assez d'éléments

                if (StackSize < list.Items.Count)
                    return EvalResult.Failure(this, Error.TooFewArgumentsError, name, "the stack does not contain enough elements.");

                // Pour chaque name on prend un item de la stack et on crée une variable avec
                // On travaille à l'envers pour que les paramètres soient dans le bon sens

                for (int i = list.Items.Count - 1; i >= 0; i--)
                {
                    var varName = list.Items[i] as MOGName;
                    var item = StackPop();

                    var r2 = VarWrite(varName.Value, item!);

                    if (r2 != EvalResult.NoError)
                        return r2;
                }

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRecord))
            {
                // Signature [id: 50 name: "SIBUE" x: 'Z'] ->vars

                var record = StackPop() as MOGRecord;

                foreach (var key in record!.Items.Keys)
                {
                    var item = record.Items[key] as MOGObject;
                    VarWrite(key as string, item);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveStackToSafeVars(string name)
        {
            // 10 "SIBUE" 'Z' [id: .number name: .string x: .name] ->safeVars -------> id=50 name="SIBUE" x='Z'

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGRecord))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            // On récupère le record de référence et ses clés

            var recf = StackPop() as MOGRecord;

            var keys = new string[recf.Keys.Count];
            var i = 0;

            foreach (var k in recf.Keys)
                keys[i++] = k as string;

            // Le record de référence ne doit porter QUE des types

            foreach (var k in keys)
            {
                if (recf.Items[k] is not MOGType)
                    return EvalResult.Failure(this, Error.BadArgumentTypeError, "reference record must have .type values.");
            }

            // La pile doit au moins contenir le nombre de clés du record de référence

            if (StackSize < recf.Items.Count)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, "the stack does not contain enough elements.");

            // On récupère toute les valeurs depuis la pile

            var values = new ArrayList();

            for (i = 0; i < keys.Length; i++)
            {
                var value = StackPop();
                values.Add(value);
            }

            // On vérifie la correspondance de types

            var index = 0;

            for (i = keys.Length - 1; i >= 0; i--)
            {
                // On lit la valeur

                var pv = values[index++] as MOGObject;

                // On récupère le type attendu

                var tv = recf.Items[keys[i]] as MOGType;

                // Si incorrect on arrête tout

                if (tv.Value != "any" && tv.Value != pv.Type.Value)
                    return EvalResult.Failure(this, Error.BadArgumentTypeError, name, $"{tv} expected but {pv.Type} found for '{keys[i]}' parameter");
            }

            // On crée les variables locales

            index = 0;

            for (i = keys.Length - 1; i >= 0; i--)
            {
                var v = values[index++] as MOGObject;
                var r = VarWrite(keys[i], v);

                if (r != EvalResult.NoError)
                    return r;
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveStackToParams(string name)
        {
            // [id: 50 name: "SIBUE" x: 'Z'] [id: .number name: .string u: (.boolean true)] ->params -------> id=50 name="SIBUE u=true"

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGRecord) || s[1] != typeof(MOGRecord))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGRecord;
            var n1 = StackPop() as MOGRecord;

            // On décompose n0 en liste de paramètes ayant type + éventuellement valeur par défaut

            var pDefinitions = new ArrayList();

            foreach (string key in n0.Keys)
            {
                // La clé porte un type ou une liste avec (type defaultValue)

                var value = n0.Items[key];

                if (value is MOGType v)
                {
                    // OK

                    var np = new ParamDefinition(key, v, null);
                    pDefinitions.Add(np);
                }
                else if (value is MOGList list)
                {
                    // La liste doit être composée de 2 élements

                    if (list.Items.Count != 2)
                        return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have 2 items (type defaultValue).");

                    // L'item 0 doit être un type

                    if (list.Items[0] is MOGType type)
                    {
                        // L'item 1 doit être une valeur du type ou sans importance si type .any

                        if (list.Items[1] is MOGObject defaultValue && (type.Value == "any" || defaultValue.Type.Value == type.Value))
                        {
                            // OK

                            var np = new ParamDefinition(key, type, defaultValue);
                            pDefinitions.Add(np);
                        }
                        else
                        {
                            return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have a value with the good type in second position.");
                        }
                    }
                    else
                    {
                        return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have a type in first position.");
                    }
                }
                else
                {
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{key}: parameter", "parameter definition is a type or a list (type defaultValue).");
                }
            }

            foreach (ParamDefinition p in pDefinitions)
            {
                if (n1.Keys.Contains(p.VarName) && n1.Items[p.VarName] is MOGObject pv)
                {
                    // On a une valeur fournie pour ce paramètre
                    // Il doit être du bon type (sauf si le type attendu est .any)

                    if (p.Type.Value == "any" || pv.Type.Value == p.Type.Value)
                    {
                        // Tout est OK
                        // La valeur a le bon type
                        // On peut prendre en compte la valeur

                        p.Value = pv;
                    }
                    else
                    {
                        return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{p.VarName}: type is invalid !", $"{p.Type} expected", $"{pv.Type} provided");
                    }
                }
                else
                {
                    // On n'a pas de valeur fournie pour ce paramètre
                    // Si on a une valeur par défaut c'est pas grave, sinon erreur !

                    if (p.Value == null)
                        return EvalResult.Failure(this, Error.BadArgumentValueError, name, $"{p.VarName}: parameter is mandatory !");
                }
            }

            // On crée les variables
            // Normalement on ne devrait pas avoir de valeur à null
            // Pour le moment on ne bloque pas, on place juste MOGNull comme valeur dans ce cas là

            EvalResult result = EvalResult.NoError;

            foreach (ParamDefinition pdef in pDefinitions)
            {
                result = VarWrite(pdef.VarName, pdef.Value ?? new MOGNull(this));

                if (result != EvalResult.NoError)
                    break;
            }

            return result;
        }

        private EvalResult PrimitiveBinaryAnd(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var n1 = StackPop() as MOGNumber;

            var result = (int)n0.Value & (int)n1.Value;

            StackPush(new MOGNumber(this, result));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveBinaryOr(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var n1 = StackPop() as MOGNumber;

            var result = (int)n0.Value | (int)n1.Value;

            StackPush(new MOGNumber(this, result));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveBinaryXor(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var n1 = StackPop() as MOGNumber;

            var result = (int)n0.Value ^ (int)n1.Value;

            StackPush(new MOGNumber(this, result));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveBinaryComplement(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var result = ~(int)n0.Value;

            StackPush(new MOGNumber(this, result));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveRightShift(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n0 = StackPop() as MOGNumber;
                var n1 = StackPop() as MOGNumber;

                int v = (int)n1.Value >> (int)n0.Value;
                StackPush(new MOGNumber(this, v));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveLeftShift(string name)
        {
            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n0 = StackPop() as MOGNumber;
                var n1 = StackPop() as MOGNumber;

                int v = (int)n1.Value << (int)n0.Value;
                StackPush(new MOGNumber(this, v));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveToFormat(string name)
        {
            // 50 "D3" ->format -----> "050"

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString) && s[1] == typeof(MOGNumber))
            {
                var format = StackPop() as MOGString;   
                var number = StackPop() as MOGNumber;

                try
                {
                    string result;
                    char specifier = format.Value.Length > 0 ? format.Value[0].ToUpper() : ' ';

                    if (specifier == 'D' || specifier == 'X')
                    {
                        result = ((int)number.Value).ToString(format.Value);
                    }
                    else
                    {
                        result = number.Value.ToString(format.Value);
                    }

                    StackPush(new MOGString(this, result));
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSub(string name)
        {
            // "ABCDE" 1 1 sub ---> "B"
            // "ABCDE" 2 0 sub   ---> "CDE"

            // (1 2 3 4 5) 1 1 sub ---> (2)
            // (1 2 3 4 5) 2 0 sub   ---> (3 4 5)

            // D:FFBBEE 0 2 sub ---> D:FFBB

            var sign = StackSign(3);

            if (sign.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (sign[0] != typeof(MOGNumber) || sign[1] != typeof(MOGNumber) || (sign[2] != typeof(MOGString) && sign[2] != typeof(MOGList) && sign[2] != typeof(MOGData) && sign[2] != typeof(MOGRef)))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var n0 = StackPop() as MOGNumber;
            var n1 = StackPop() as MOGNumber;
            var n2 = StackPop();

            var start = (int)n1.Value;
            var count = (int)n0.Value;

            if (start < 0 || count < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            if (n2 is MOGString s)
            {
                if (start >= s.Value.Length)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = s.Value.Length;

                if (start + count >= s.Value.Length)
                    count = s.Value.Length - start;

                try
                {
                    StackPush(new MOGString(this, s.Value.Substring(start, count)));
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, ex.Message);
                }
            }
            else if (n2 is MOGList l)
            {
                if (start < 0 || start >= l.Items.Count)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = l.Items.Count;

                if (start + count >= l.Items.Count)
                    count = l.Items.Count - start;

                var l2 = new MOGList(this);

                for (int i = 0; i < count; i++)
                    l2.Items.Add(l.Items[start + i]);

                StackPush(l2);
                return EvalResult.NoError;
            }
            else if (n2 is MOGData d)
            {
                if (start < 0 || start >= d.Items.Length)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = d.Items.Length;

                if (start + count >= d.Items.Length)
                    count = d.Items.Length - start;

                var items = new byte[count];

                for (int i = 0; i < count; i++)
                    items[i] = d.Items[start + i];

                var d2 = new MOGData(this, items);

                StackPush(d2);

                return EvalResult.NoError;
            }
            else if (n2 is MOGRef r)
            {
                var value = VarRead(r.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name, r.ToString());

                StackPush(value);
                StackPush(n1);
                StackPush(n0);

                return PrimitiveSub(name);
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveToNumber(string name)
        {
            // string ->num

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString))
            {
                var @string = StackPop() as MOGString;

                if (float.TryParse(@string.Value, out var value))
                {
                    StackPush(new MOGNumber(this, value));
                    return EvalResult.NoError;
                }

                return EvalResult.Failure(this, Error.BadArgumentValueError, name, @string.ToString());
            }       

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        #region SKILLS

        private EvalResult PrimitiveGetSkills(string name)
        {
            var list = new MOGList(this);

            foreach (var skill in _skills)
                list.AddItem(new MOGName(this, skill));

            StackPush(list);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveHasSkill(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var skillName = StackPop() as MOGName;
            var skillValue = skillName.Value.ToUpper();

            foreach (var skill in _skills)
            {
                if (skill == skillValue)
                {
                    StackPush(new MOGBoolean(this, true));
                    return EvalResult.NoError;
                }
            }

            StackPush(new MOGBoolean(this, false));

            return EvalResult.NoError;
        }

        #endregion

        #region FLAGS

        private EvalResult PrimitiveFlagSet(string name)
        {
            // 'name' flag.set

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var flagName = StackPop() as MOGName;

            if (!_flags.Contains(flagName.Value))
                _flags.Add(flagName.Value);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveFlagClear(string name)
        {
            // 'name' flag.clear

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var flagName = StackPop() as MOGName;

            if (_flags.Contains(flagName.Value))
                _flags.Remove(flagName.Value);

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveFlagIsSet(string name)
        {
            // 'name' flag.isSet

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var flagName = StackPop() as MOGName;
            var v = _flags.Contains(flagName.Value);
            StackPush(new MOGBoolean(this, v));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveFlagIsClear(string name)
        {
            // 'name' flag.isClear

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var flagName = StackPop() as MOGName;
            var v = _flags.Contains(flagName.Value);
            StackPush(new MOGBoolean(this, !v));

            return EvalResult.NoError;
        }

        #endregion

        #region GPIO

        private EvalResult PrimitiveGpioModeInput(string name) => SetPinMode(name, PinMode.Input);

        private EvalResult PrimitiveGpioSetModeInputPullDown(string name) => SetPinMode(name, PinMode.InputPullDown);

        private EvalResult PrimitiveGpioSetModeInputPullUp(string name) => SetPinMode(name, PinMode.InputPullUp);

        private EvalResult PrimitiveGpioSetModeOutput(string name) => SetPinMode(name, PinMode.Output);

        private EvalResult PrimitiveGpioPinWriteHigh(string name) => GpioPinWrite(name, PinValue.High);

        private EvalResult PrimitiveGpioPinWriteLow(string name) => GpioPinWrite(name, PinValue.Low);

        private EvalResult PrimitiveGpioPinRead(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var numPin = StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(this, Error.GpioUnknownPinError, name);

            var e = pin.Read();
            StackPush(new MOGNumber(this, e == PinValue.High ? 1 : 0));

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveGpioPinToggle(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var numPin = StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(this, Error.GpioUnknownPinError, name);

            pin.Toggle();

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveGpioPinClose(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var numPin = StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (!ClosePin((int)numPin.Value))
                return EvalResult.Failure(this, Error.GpioUnknownPinError, name);

            return EvalResult.NoError;
        }

        #endregion

        #region I2C

        private EvalResult PrimitiveI2cOpen(string name)
        {
            // 'name' bus address i2c.open

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber) || s[2] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var address = StackPop() as MOGNumber;
            var bus = StackPop() as MOGNumber;
            var deviceName = StackPop() as MOGName;

            int busNumber = (int)bus.Value;

            if (busNumber < 1 || busNumber > 2)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C bus number must be between 1 and 2");

            int addressNumber = (int)address.Value;

            if (addressNumber < 0 || addressNumber > 127)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C address number must be between 0 and 127");

            if (_i2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(this, Error.I2cDeviceAlreadyOpenedError, name, $"I2C device (bus {busNumber}, address {addressNumber:X2}) is already opened");

            try
            {
                var i2cSettings = new I2cConnectionSettings(busNumber, addressNumber, I2cBusSpeed.FastMode);
                var i2cDevice = I2cDevice.Create(i2cSettings);

                _i2cDevices.Add(deviceName.Value, i2cDevice);
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.I2cDeviceOpenError, name, $"failed to open I2C device (bus {busNumber}, address {addressNumber:X2})", ex.Message);
            }

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveI2cClose(string name)
        {
            // name i2c.close   

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var deviceName = StackPop() as MOGName;

            if (!_i2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(this, Error.I2cUnknownDeviceNameError, name);

            var i2cDevice = _i2cDevices[deviceName.Value] as I2cDevice;
            _i2cDevices.Remove(deviceName.Value);

            i2cDevice.Dispose();

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveI2cWrite(string name)
        {
            // name data i2c.write

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGName))
            {   
                var data = StackPop() as MOGData;
                var deviceName = StackPop() as MOGName;

                if (!_i2cDevices.Contains(deviceName.Value))
                    return EvalResult.Failure(this, Error.I2cUnknownDeviceNameError, name);

                var i2cDevice = _i2cDevices[deviceName.Value] as I2cDevice;

                try
                {
                    var r = i2cDevice.Write(data.ToSpanByte());

                    if (r.Status == I2cTransferStatus.FullTransfer)
                    {
                        return EvalResult.NoError;
                    }
                    else
                    {
                        return EvalResult.Failure(this, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                    }
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGName))
            {
                var @ref = StackPop() as MOGRef;
                var deviceName = StackPop() as MOGName;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name.ToString());

                StackPush(deviceName);
                StackPush(value);

                var r = PrimitiveI2cWrite(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveI2cRegisterWrite(string name)
        {
            // name register data i2c.write

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGName))
            {
                var data = StackPop() as MOGData;
                var register = StackPop() as MOGNumber;
                var deviceName = StackPop() as MOGName;

                if (!_i2cDevices.Contains(deviceName.Value))
                    return EvalResult.Failure(this, Error.I2cUnknownDeviceNameError, name);

                if (register.Value < 0 || register.Value > 255)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C register number must be between 0 and 255");

                var registerAddress = (byte)register.Value;

                var i2cDevice = _i2cDevices[deviceName.Value] as I2cDevice;

                try
                {
                    byte[] buffer = new byte[1 + data.Items.Length];
                    buffer[0] = registerAddress;
                    Array.Copy(data.Items, 0, buffer, 1, data.Items.Length);
                    var span = new SpanByte(buffer);
                    var r = i2cDevice.Write(span);

                    if (r.Status != I2cTransferStatus.FullTransfer)
                        return EvalResult.Failure(this, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");

                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGName))
            {
                var @ref = StackPop() as MOGRef;
                var register = StackPop() as MOGNumber;
                var deviceName = StackPop() as MOGName;

                var value = VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(this, Error.UnknownNameError, name);

                StackPush(deviceName);
                StackPush(register);
                StackPush(value);   

                var r = PrimitiveI2cRegisterWrite(name);

                if (r.IsError)
                    return r;

                StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveI2cRead(string name)
        {
            // name length i2c.read

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var length = StackPop() as MOGNumber;
            var deviceName = StackPop() as MOGName;

            if (!_i2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(this, Error.I2cUnknownDeviceNameError, name);

            if (length.Value < 0 || length.Value > 255)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C read length must be between 0 and 255");

            var i2cDevice = _i2cDevices[deviceName.Value] as I2cDevice;

            try
            {
                byte[] buffer = new byte[(int)length.Value];
                var span = new SpanByte(buffer);
                var r = i2cDevice.Read(span);

                if (r.Status == I2cTransferStatus.FullTransfer)
                {
                    var mogData = new MOGData(this, buffer);
                    StackPush(mogData);

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(this, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                }
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", ex.Message);
            }
        }

        private EvalResult PrimitiveI2cRegisterRead(string name)
        {
            // name register length i2c.read

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber) || s[2] != typeof(MOGName))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var length = StackPop() as MOGNumber;
            var register = StackPop() as MOGNumber;
            var deviceName = StackPop() as MOGName;

            if (!_i2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(this, Error.I2cUnknownDeviceNameError, name);

            if (register.Value < 0 || register.Value > 255)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C register number must be between 0 and 255");

            var registerAddress = (byte)register.Value;

            if (length.Value < 0 || length.Value > 255)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C read length must be between 0 and 255");

            var count = (int)length.Value;

            var i2cDevice = _i2cDevices[deviceName.Value] as I2cDevice;

            try
            {
                byte[] writeBuffer = new byte[] { registerAddress };
                byte[] readBuffer = new byte[count];

                SpanByte writeSpan = new SpanByte(writeBuffer);
                SpanByte readSpan = new SpanByte(readBuffer);

                I2cTransferResult r = i2cDevice.WriteRead(writeSpan, readSpan);

                if (r.Status == I2cTransferStatus.FullTransfer)
                {
                    var mogData = new MOGData(this, readBuffer);
                    StackPush(mogData);

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(this, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                }
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(this, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", ex.Message);
            }
        }

        private EvalResult PrimitiveI2cScan(string name)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var bus = StackPop() as MOGNumber;
            int busNumber = (int)bus.Value;

            var list = new MOGList(this);

            byte[] probe = new byte[1];
            SpanByte span = new SpanByte(probe);

            for (int address = 0x08; address <= 0x77; address++)
            {
                using (I2cDevice i2c = new(new I2cConnectionSettings(busNumber, address)))
                {
                    var res = i2c.Write(span);

                    if (res.Status == I2cTransferStatus.FullTransfer)
                        list.AddItem(new MOGNumber(this, address));
                }
            }

            StackPush(list);

            return EvalResult.NoError;
        }

        #endregion

        #region PWM

        private EvalResult PrimitivePwmOpen(string name)
        {
            // 'name' pin frequency dutyCycle pwm.open

            var s = StackSign(4);
            
            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGName))
            {
                var dutyCycle = StackPop() as MOGNumber;
                var frequency = StackPop() as MOGNumber;
                var pin = StackPop() as MOGNumber;
                var pwmName = StackPop() as MOGName;    

                if (dutyCycle.Value < 0 || dutyCycle.Value > 100)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, "PWM duty cycle must be between 0 and 100");
                
                if (frequency.Value <= 0)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, "PWM frequency must be greater than 0");
                
                if (_pwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(this, Error.PwmAlreadyOpenedError, name);
                
                int nPin = (int)pin.Value;

                try
                {
                    var pwmChannel = PwmChannel.CreateFromPin(nPin, (int)frequency.Value, dutyCycle.Value / 100.0);
                    
                    if (pwmChannel == null)
                        return EvalResult.Failure(this, Error.PwmOpenError, name, $"failed to open PWM on pin {nPin}");

                    _pwmChannels.Add(pwmName.Value, pwmChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.PwmOpenError, name, $"failed to open PWM on pin {nPin}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitivePwmClose(string name)
        {
            // 'name' pwm.close

            var s = StackSign(1);
            
            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = StackPop() as MOGName;

                if (!_pwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(this, Error.PwmUnknownNameError, name);
                
                var pwmChannel = _pwmChannels[pwmName.Value] as PwmChannel;               
                pwmChannel.Stop();
                pwmChannel.Dispose();
               
                _pwmChannels.Remove(pwmName.Value);
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitivePwmStart(string name)
        {
            // name pwm.start

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = StackPop() as MOGName;

                if (!_pwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(this, Error.PwmUnknownNameError, name);

                var pwmChannel = _pwmChannels[pwmName.Value] as PwmChannel;
                pwmChannel.Start();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitivePwmStop(string name)
        {
            // name pwm.stop

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = StackPop() as MOGName;

                if (!_pwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(this, Error.PwmUnknownNameError, name);

                var pwmChannel = _pwmChannels[pwmName.Value] as PwmChannel;
                pwmChannel.Stop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region ADC

        private EvalResult PrimitiveAdcOpen(string name)
        {

            // 'name' channel adc.open

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGName))
            {
                var channel = StackPop() as MOGNumber;
                var adcName = StackPop() as MOGName;
             
                if (_adcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(this, Error.AdcAlreadyOpenedError, name);

                int nChannel = (int)channel.Value;

                try
                {
                    if (_adcController == null)
                        _adcController = new AdcController();

                    var adcChannel = _adcController.OpenChannel(nChannel);

                    _adcChannels.Add(adcName.Value, adcChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.AdcOpenError, name, $"failed to open ADC channel {nChannel}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveAdcClose(string name)
        {
            // 'name' adc.close

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = StackPop() as MOGName;

                if (!_adcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(this, Error.AdcUnknownNameError, name);

                var adcChannel = _adcChannels[adcName.Value] as AdcChannel;
                adcChannel.Dispose();

                _adcChannels.Remove(adcName.Value);

                if (_adcChannels.Count == 0)
                    _adcController = null;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveAdcReadValue(string name)
        {
            // name adc.readValue

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = StackPop() as MOGName;

                if (!_adcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(this, Error.AdcUnknownNameError, name);

                var adcChannel = _adcChannels[adcName.Value] as AdcChannel;
                var value = adcChannel.ReadValue();

                StackPush(new MOGNumber(this, value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveAdcGetMaxValue(string name)
        {
            // adc.maxValue

            if (_adcController == null)
                _adcController = new AdcController();

            var maxValue = _adcController.MaxValue;
            StackPush(new MOGNumber(this, maxValue));
            
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveAdcGetResolutionInBits(string name)
        {
            // adc.resolutionInBits

            if (_adcController == null)
                _adcController = new AdcController();

            var resolutionInBits = _adcController.ResolutionInBits;
            StackPush(new MOGNumber(this, resolutionInBits));

            return EvalResult.NoError;
        }

        #endregion

        #region SSD1306 OLED SCREEN

        private EvalResult PrimitiveSsd1306Init(string name)
        {
            // bus address ssd1306.init

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var address = StackPop() as MOGNumber;
                var bus = StackPop() as MOGNumber;

                if (_ssd1306 != null)
                    return EvalResult.Failure(this, Error.Ssd1306IsOpenedError, name);

                int busNumber = (int)bus.Value;
                int addressNumber = (int)address.Value;

                if (busNumber < 1 || busNumber > 2)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C bus number must be between 1 and 2");
                
                if (addressNumber < 0 || addressNumber > 127)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name, "I2C address number must be between 0 and 127");

                if (_ssd1306 != null)
                    return EvalResult.Failure(this, Error.Ssd1306IsOpenedError, name);

                try
                {
                    I2cConnectionSettings settings = new(busNumber, addressNumber, I2cBusSpeed.FastMode);
                    I2cDevice i2cDevice = I2cDevice.Create(settings);

                    _ssd1306 = new(i2cDevice, DisplayResolution.OLED128x64);
                    _ssd1306.ClearScreen();
                }
                catch (Exception ex)
                {
                    if (_ssd1306 != null)
                    {
                        _ssd1306.Dispose();
                        _ssd1306 = null;
                    }

                    return EvalResult.Failure(this, Error.Ssd1306InitError, name, "failed to initialize ssd1306 display", $"bus {busNumber}", $"address {addressNumber:X2}", ex.Message);
                }
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306Close(string name)
        {
            if (_ssd1306 != null)
            { 
                _ssd1306.Dispose();
                _ssd1306 = null;
            }   

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveSsd1306Clear(string name)
        {
            if (_ssd1306 == null)
                return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);
            
            _ssd1306.ClearScreen();
            
            return EvalResult.NoError;
        }

        private EvalResult PrimitiveSsd1306PrintString(string name)
        {
            // x y text size center ssd1306.printString

            var s = StackSign(5);
            
            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = StackPop() as MOGBoolean;
                var size = StackPop() as MOGNumber;
                var text = StackPop() as MOGString;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    if (_ssd1306.Font == null)
                        _ssd1306.Font = new BasicFont();

                    _ssd1306.Write((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;  
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);  
        }

        private EvalResult PrimitiveSsd1306DrawString(string name)
        {
            // x y text size center ssd1306.drawString

            var s = StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = StackPop() as MOGBoolean;
                var size = StackPop() as MOGNumber;
                var text = StackPop() as MOGString;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    if (_ssd1306.Font == null)
                        _ssd1306.Font = new BasicFont();

                    _ssd1306.DrawString((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306Refresh(string name)
        {
            if (_ssd1306 == null)
                return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

            _ssd1306.Display();

            return EvalResult.NoError;
        }

        private EvalResult PrimitiveSsd1306DrawPixel(string name)
        {
            // x y true ssd1306.drawPixel   

            var s = StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber))
            {
                var set = StackPop() as MOGBoolean;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;
                
                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    _ssd1306.DrawPixel((int)x.Value, (int)y.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306DrawHorizontalLine(string name)
        {
            // x y len true ssd1306.drawHorizontalLine  

            var s = StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = StackPop() as MOGBoolean;
                var len = StackPop() as MOGNumber;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    _ssd1306.DrawHorizontalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306DrawHVerticalLine(string name)
        {
            // x y len true ssd1306.drawVerticalLine 

            var s = StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = StackPop() as MOGBoolean;
                var len = StackPop() as MOGNumber;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    _ssd1306.DrawVerticalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306DrawRectangle(string name)
        {
            // x y w h true ssd1306.drawRectangle

            var s = StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = StackPop() as MOGBoolean;
                var height = StackPop() as MOGNumber;
                var width = StackPop() as MOGNumber;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    int vx = (int)x.Value;
                    int vy = (int)y.Value;
                    int vw = (int)width.Value;
                    int vh = (int)height.Value;

                    _ssd1306.DrawHorizontalLine(vx, vy, vw,  set.Value);
                    _ssd1306.DrawVerticalLine(vx + vw - 1,vy, vh, set.Value);
                    _ssd1306.DrawHorizontalLine(vx, vy + vh - 1, vw, set.Value);
                    _ssd1306.DrawVerticalLine(vx, vy, vh, set.Value);
                    
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306DrawFilledRectangle(string name)
        {
            // x y w h true ssd1306.drawFilledRectangle

            var s = StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = StackPop() as MOGBoolean;
                var height = StackPop() as MOGNumber;
                var width = StackPop() as MOGNumber;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    _ssd1306.DrawFilledRectangle((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value,set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        private EvalResult PrimitiveSsd1306DrawBitmap(string name)
        {
            // x y w h data size ssd1306.drawBitmap

            var s = StackSign(6);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber) && s[5] == typeof(MOGNumber))
            {
                var size = StackPop() as MOGNumber;
                var data = StackPop() as MOGData;
                var height = StackPop() as MOGNumber;
                var width = StackPop() as MOGNumber;
                var y = StackPop() as MOGNumber;
                var x = StackPop() as MOGNumber;

                if (_ssd1306 == null)
                    return EvalResult.Failure(this, Error.Ssd1306IsClosedError, name);

                try
                {
                    _ssd1306.DrawBitmap((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value, data.Items, (byte)size.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region DEVICE

        private EvalResult PrimitiveDeviceSetPinFunction(string name)
        {
            // pin setvalue device.setPin

            var s = StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);  

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var setValue = StackPop() as MOGNumber;
                var pin = StackPop() as MOGNumber;  

                if (pin.Value < 0)
                    return EvalResult.Failure(this, Error.BadArgumentValueError, name);

                if (SystemInfo.Platform == "ESP32")
                {
                    try
                    {
                        nanoFramework.Hardware.Esp32.Configuration.SetPinFunction((int)pin.Value, (nanoFramework.Hardware.Esp32.DeviceFunction)(int)setValue.Value);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(this, Error.PlatformNotSupportedError, name, ex.Message);
                    }

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(this, Error.PlatformNotSupportedError, name);
                }
            }

            return EvalResult.Failure(this, Error.BadArgumentTypeError, name);
        }

        #endregion

        #endregion

        #region VARS

        public EvalResult VarWrite(string name, MOGObject value)
        {
            // This name is used by a func ?

            if (_functions.Contains(name))
                return EvalResult.Failure(this, Error.NameAlreadyUsedByFunctionError);

            bool r = false;

            if (name.StartsWith("$"))
            {
                // Global var

                var context = _varsContext[0] as VarContext;
                r = context.Write(name, value);
            }
            else
            {
                // Local var

                if (_currentLocalVarsContext != null)
                    r = _currentLocalVarsContext.Write(name, value);
            }

            if (!r)
            {
                return EvalResult.Failure(this, Error.UnableToWriteValueError, "certainly bad type error");
            }
            else
            {
                return EvalResult.NoError;
            }
        }

        public MOGObject VarRead(string name, bool clone = true)
        {
            MOGObject value = null;

            if (name.StartsWith("$"))
            {
                var context = _varsContext[0] as VarContext;
                value = context.Read(name, clone);
            }
            else
            {
                if (_currentLocalVarsContext != null)
                    value = _currentLocalVarsContext.Read(name, clone);
            }

            return value;
        }

        public bool VarExists(string name)
        {
            if (name.StartsWith("$"))
            {
                var context = _varsContext[0] as VarContext;
                return context.Exists(name);
            }
            else
            {
                if (_currentLocalVarsContext != null)
                {
                    return _currentLocalVarsContext.Exists(name);
                }
                else
                {
                    return false;
                }
            }
        }

        public bool VarPurge(string name)
        {
            if (name.StartsWith("$"))
            {
                var context = _varsContext[0] as VarContext;
                return context.Purge(name);
            }
            else
            {
                if (_currentLocalVarsContext != null)
                {
                    return _currentLocalVarsContext.Purge(name);
                }
                else
                {
                    return false;
                }
            }
        }

        public void VarPushContext(string name)
        {
            _currentLocalVarsContext = new VarContext(name);
            _varsContext.Add(_currentLocalVarsContext);
        }

        public void VarPopContext()
        {
            if (_varsContext.Count > 1)
            {
                _varsContext.RemoveAt(_varsContext.Count - 1);

                if (_varsContext.Count > 0)
                {
                    _currentLocalVarsContext = _varsContext[_varsContext.Count - 1] as VarContext;
                }
                else
                {
                    _currentLocalVarsContext = null;
                }
            }
        }

        public string[] GetGlobalVarNames()
        {
            var context = _varsContext[0] as VarContext;
            var names = new string[context.Keys.Length];
            context.Keys.CopyTo(names, 0);
            return names;
        }

        public string[] GetLocalVarNames()
        {
            if (_varsContext.Count < 2)
                return new string[0];

            var names = new string[_currentLocalVarsContext.Keys.Length];
            _currentLocalVarsContext.Keys.CopyTo(names, 0);
            return names;
        }

        #endregion

        #region FUNCTIONS

        public MOGFunction GetFunction(string name)
        {
            if (_functions.Contains(name))
                return _functions[name] as MOGFunction;

            return null;
        }

        #endregion

        #region FIREOBJECTS

        public void RegisterFireObject(MOGFireObject fireObject)
        {
            lock (_fireObjectsQueueLock)
                _fireObjectsQueue.Enqueue(fireObject);
        }

        public void ClearWaitingFireObjects()
        {
            lock (_fireObjectsQueueLock)
                _fireObjectsQueue.Clear();
        }

        public void ClearTimers()
        {
            foreach (var key in _timers.Keys)
            {
                var timer = _timers[key] as MOGTimer;
                timer.Stop();
            }

            _timers.Clear();
        }

        public bool HasWaitingFireObjects => !_disableInterrupts && _fireObjectsQueue.Count > 0;

        public EvalResult ExecuteWaitingFireObjects()
        {
            var result = EvalResult.NoError;

            if (!_disableInterrupts && _fireObjectsQueue.Count > 0)
            {
                MOGFireObject fireObject = null;

                lock (_fireObjectsQueueLock)
                    fireObject = _fireObjectsQueue.Dequeue() as MOGFireObject;

                AddNewStack();

                result = fireObject.Function.Execute();

                RemoveLastStack();
            }

            return result;
        }

        #endregion

        #region TIMERS

        public EvalResult PurgeTimer(string name)
        {
            if (_timers.Contains(name))
            {
                var timer = _timers[name] as MOGTimer;
                timer.Stop();
                _timers.Remove(name);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.UnknownNameError, $"unabled to purge unknown '{name}' timer.");
        }

        public EvalResult CreateNewTimer(string name, int interval, bool isCyclic, MOGFunction function, bool isLaterTimer = false)
        {
            if (_timers.Contains(name))
                return EvalResult.Failure(this, Error.NameAlreadyExistsError, $"timer '{name}' already exists.");

            if (interval < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, "timer interval must be a positive value.");

            var timer = new MOGTimer(this, name, interval, isCyclic, function, isLaterTimer);
            _timers.Add(name, timer);

            return EvalResult.NoError;
        }

        #endregion

        #region EVENT FUNCTIONS

        public bool EventExists(string name) => _events.Contains(name);

        public MOGEvent GetEvent(string name)
        {
            if (_events.Contains(name))
                return _events[name] as MOGEvent;

            return null;
        }

        public EvalResult CreateNewEvent(string name, MOGFunction function)
        {
            if (_events.Contains(name))
                return EvalResult.Failure(this, Error.NameAlreadyExistsError, $"event '{name}' already exists.");

            var @event = new MOGEvent(this, name, function);
            _events.Add(name, @event);

            return EvalResult.NoError;
        }

        public EvalResult PurgeEvent(string name)
        {
            if (_events.Contains(name))
            {
                _events.Remove(name);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.UnknownNameError, $"unabled to purge unknown '{name}' event.");
        }

        public EvalResult FireEvent(string name, MOGObject eventData)
        {
            lock (_fireEventLock)
            {
                try
                {
                    if (_events.Contains(name))
                    {
                        var @event = _events[name] as MOGEvent;
                        var primitiveSto = new MOGPrimitive(this, "STO");

                        @event = @event.Clone() as MOGEvent;

                        if (@event.Function.Items == null)
                        {
                            if (!@event.Function.Parse())
                                return EvalResult.Failure(this, Error.UnableToFireEventError, $"unable to fire event '{name}'", "parse error");
                        }

                        @event.Function.Items.Insert(0, primitiveSto);
                        @event.Function.Items.Insert(0, new MOGName(this, "eventData"));
                        @event.Function.Items.Insert(0, eventData);

                        RegisterFireObject(@event);
                    }

                    return EvalResult.NoError;
                }
                catch
                {
                    return EvalResult.Failure(this, Error.UnableToFireEventError, $"unable to fire event '{name}'.");
                }
            }
        }

        public void ClearEvents()
        {
            _events.Clear();
        }

        #endregion

        #region GPIO

        private GpioPin GetPin(int pinNumber)
        {
            if (_openPins.Contains(pinNumber))
                return _openPins[pinNumber] as GpioPin;

            return null;
        }

        private bool ClosePin(int pinNumber)
        {
            if (_openPins.Contains(pinNumber))
            {
                var pin = _openPins[pinNumber] as GpioPin;
                _openPins.Remove(pin);
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
                return true;
            }

            return false;
        }

        private EvalResult GpioPinWrite(string name, PinValue pinValue)
        {
            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var numPin = StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var pin = GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(this, Error.GpioUnknownPinError, name);

            pin.Write(pinValue);

            return EvalResult.NoError;
        }

        private void CleanupOpenPins()
        {
            foreach (int pinNumber in _openPins.Keys)
            {
                var pin = _openPins[pinNumber] as GpioPin;
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
            }

            _openPins.Clear();
        }

        private EvalResult SetPinMode(string name, PinMode mode)
        {
            // Used by all pin mode known
            // 4 gpio.setMode.xxx

            var s = StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(this, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(this, Error.BadArgumentTypeError, name);

            var numPin = StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (_openPins.Contains(nPin))
            {
                var pin = _openPins[nPin] as GpioPin;
                pin.SetPinMode(mode);
            }
            else
            {
                GpioPin newPin = _gpioController.OpenPin(nPin, mode);
                _openPins.Add(nPin, newPin);

                newPin.ValueChanged += GpioPin_ValueChanged;
            }
            return EvalResult.NoError;
        }

        private void GpioPin_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            var eventType = e.ChangeType == PinEventTypes.Rising ? 1 : 0;

            var record = new MOGRecord(this);
            record.SetItem("pin", new MOGNumber(this, e.PinNumber));
            record.SetItem("eventType", new MOGNumber(this, eventType));

            FireEvent("GPIO_PIN_CHANGED", record);
        }

        #region I2C

        private void CleanupI2cDevices()
        {
            foreach (var key in _i2cDevices.Keys)
            {
                var i2cDevice = _i2cDevices[key] as I2cDevice;
                i2cDevice.Dispose();
            }

            _i2cDevices.Clear();
        }

        private void CleanupAdcChannels()
        {
            foreach (var key in _adcChannels.Keys)
            {
                var adcChannel = _adcChannels[key] as AdcChannel;
                adcChannel.Dispose();
            }

            _adcChannels.Clear();
            _adcController = null;
        }

        private void CleanupPwmChannels()
        {
            foreach (var key in _pwmChannels.Keys)
            {
                var pwmChannel = _pwmChannels[key] as PwmChannel;

                pwmChannel.Stop();
                pwmChannel.Dispose();
            }

            _pwmChannels.Clear();
        }

        #endregion

        #endregion

        #region INTERNALS CLASSES

        private class ParamDefinition
        {
            public string VarName { get; set; }

            public MOGType Type { get; set; }

            public MOGObject Value { get; set; }

            public ParamDefinition(string varName, MOGType type, MOGObject value)
            {
                VarName = varName;
                Type = type;
                Value = value;
            }
        }

        #endregion
    }
}
