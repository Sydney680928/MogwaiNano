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
using System.IO;
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

        private delegate EvalResult PrimitiveDelegate(MogwaiNanoEngine engine, string name);

        public static Hashtable Primitives = new(150);

        private ArrayList _stacks = new();
        private MOGStack _currentStack = new();
        private Hashtable _events = new();
        private Hashtable _types = new(12);
        private ArrayList _varsContext = new();
        private Queue _fireObjectsQueue = new();
        private object _fireObjectsQueueLock = new();
        private object _fireEventLock = new();
        private VarContext _currentLocalVarsContext;        
        private AutoResetEvent _runSignal = new(false);
        private string _pendingRunCode;
        private bool _pendingDebugMode;
        private Thread _runThread;                  
        private static readonly string[] _skills = { "GPIO", "I2C", "SSD1306", "PWM", "ADC", "UNITS", "TASKS", "EVENTS", "TIMERS" };      
        private EvalResult _lastResult;
        private Error _lastError;
        private int _iterationCount = 0;
        private object _lastResultLock = new();
        private Parser _parser;

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

        public string Name { get; init; }

        public MogwaiNanoEngine MotherEngine { get; set; }

        public bool DisableInterrupts { get; set; }

        public Hashtable Functions { get; } = new(3);

        public Hashtable Timers { get; } = new(3);

        public ArrayList Flags { get; } = new();

        public Hashtable Stopwatches { get; } = new(2);

        public Hashtable OpenedPins { get; } = new(3);

        public GpioController GpioController { get; } = new();

        public Hashtable I2cDevices { get; } = new(2);

        public Hashtable PwmChannels { get; } = new(2);

        public Hashtable AdcChannels { get; } = new(2);

        public AdcController AdcController { get; } = new();

        public Ssd1306 Ssd1306 { get; set; }

        public Hashtable Tasks { get; } = new(2);

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

        public bool HaltRequested { get; set; }

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

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

        public static string[] Skills => _skills;

        public bool FrugalMode { get; set; } = false;

        public MOGObject TaskResult { get; set; }

        public bool IsTask { get; init; }

        static MogwaiNanoEngine()
        {
            // load primitives

            RegisterPrimitives();
        }

        public MogwaiNanoEngine(string name, MogwaiNanoEngine motherEngine = null)
        {
            Name = name;
            TaskResult = new MOGNull(this);

            if (motherEngine != null)
            {
                MotherEngine = motherEngine;
                Delegate = motherEngine.Delegate;
                IsTask = true;
            }

            // Create general parser 

            _parser = new Parser(this); 

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

            // Create vars context
            // Context zéro = Global vars

            _varsContext.Add(new VarContext("GLOBAL"));

            // Create and start running thread if not a task

            if (!IsTask)
            {
                _runThread = new Thread(RunLoop);
                _runThread.Start();
            }
        }

        ~MogwaiNanoEngine()
        {
            Debug.WriteLine($"MogwaiNanoEngine '{Name}' is being finalized.");

            if (_runThread != null)
            {
                _runThread.Abort();
                _runThread.Join();
                _runThread = null;
            }

            Reset();
        }   


        public ArrayList Parse(string code) => _parser.Parse(code);

        public bool IsPrimitive(string name) => Primitives.Contains(name);

        public MOGType GetType(string name)
        {
            if (_types.Contains(name))
                return _types[name] as MOGType;

            return null;
        }

        public static EvalResult ExecutePrimitive(MogwaiNanoEngine engine, string name)
        {
            var p = Primitives[name] as PrimitiveDelegate;

            if (p == null)
                return EvalResult.Failure(engine, Error.UnknownWordError, name);

            return p(engine, name);
        }

        private static void RegisterPrimitives()
        {
            Primitives.Add("->type", new PrimitiveDelegate(PrimitiveGetType));
            Primitives.Add("eval", new PrimitiveDelegate(PrimitiveEval));

            Primitives.Add("+", new PrimitiveDelegate(PrimitivePlus));
            Primitives.Add("-", new PrimitiveDelegate(PrimitiveMathSubstraction));
            Primitives.Add("*", new PrimitiveDelegate(PrimitiveMathMultiplication));
            Primitives.Add("/", new PrimitiveDelegate(PrimitiveMathDivision));
            Primitives.Add("floor", new PrimitiveDelegate(PrimitiveMathFloor));
            Primitives.Add("mod", new PrimitiveDelegate(PrimitiveMathModulo));

            Primitives.Add("->data", new PrimitiveDelegate(PrimitiveToData));
            Primitives.Add("->bcd", new PrimitiveDelegate(PrimitiveDecimalToBcd));
            Primitives.Add("bcd->", new PrimitiveDelegate(PrimitiveBcdToDecimal));

            Primitives.Add("->vars", new PrimitiveDelegate(PrimitiveStackToVars));
            Primitives.Add("->safeVars", new PrimitiveDelegate(PrimitiveStackToSafeVars));
            Primitives.Add("->params", new PrimitiveDelegate(PrimitiveStackToParams));

            Primitives.Add("makeData", new PrimitiveDelegate(PrimitiveMakeData));

            Primitives.Add("clear", new PrimitiveDelegate(PrimitiveStackClear));
            Primitives.Add("swap", new PrimitiveDelegate(PrimitiveStackSwap));
            Primitives.Add("dup", new PrimitiveDelegate(PrimitiveStackDup));
            Primitives.Add("drop", new PrimitiveDelegate(PrimitiveStackDrop));

            Primitives.Add("break", new PrimitiveDelegate(PrimitiveBreak));

            Primitives.Add("wait", new PrimitiveDelegate(PrimitiveWait));
            Primitives.Add("get", new PrimitiveDelegate(PrimitiveGet));
            Primitives.Add("set", new PrimitiveDelegate(PrimitiveSet));
            Primitives.Add("size", new PrimitiveDelegate(PrimitiveSize));
            Primitives.Add("purge", new PrimitiveDelegate(PrimitivePurge));
            Primitives.Add("exists", new PrimitiveDelegate(PrimitiveExists));

            Primitives.Add("DI", new PrimitiveDelegate(PrimitiveDI));
            Primitives.Add("EI", new PrimitiveDelegate(PrimitiveEI));

            Primitives.Add("==", new PrimitiveDelegate(PrimitiveConditionEqual));
            Primitives.Add("!=", new PrimitiveDelegate(PrimitiveConditionNotEqual));
            Primitives.Add("<", new PrimitiveDelegate(PrimitiveConditionInferior));
            Primitives.Add(">", new PrimitiveDelegate(PrimitiveConditionSuperior));
            Primitives.Add("<=", new PrimitiveDelegate(PrimitiveConditionInferiorOrEqual));
            Primitives.Add(">=", new PrimitiveDelegate(PrimitiveConditionSuperiorOrEqual));
            Primitives.Add("not", new PrimitiveDelegate(PrimitiveNot));
            Primitives.Add("isnull", new PrimitiveDelegate(PrimitiveConditionIsNull));
            Primitives.Add("and", new PrimitiveDelegate(PrimitiveConditionAnd));
            Primitives.Add("or", new PrimitiveDelegate(PrimitiveConditionOr));
            Primitives.Add("xor", new PrimitiveDelegate(PrimitiveConditionXor));

            Primitives.Add("&", new PrimitiveDelegate(PrimitiveBinaryAnd));
            Primitives.Add("|", new PrimitiveDelegate(PrimitiveBinaryOr));
            Primitives.Add("^", new PrimitiveDelegate(PrimitiveBinaryXor));
            Primitives.Add("~", new PrimitiveDelegate(PrimitiveBinaryComplement));
            Primitives.Add("<<", new PrimitiveDelegate(PrimitiveLeftShift));
            Primitives.Add(">>", new PrimitiveDelegate(PrimitiveRightShift));

            Primitives.Add("console.println", new PrimitiveDelegate(PrimitiveConsolePrintLn));
            Primitives.Add("?", new PrimitiveDelegate(PrimitiveConsolePrintLn));
            Primitives.Add("console.print", new PrimitiveDelegate(PrimitiveConsolePrint));
            Primitives.Add("??", new PrimitiveDelegate(PrimitiveConsolePrint));

            Primitives.Add("->format", new PrimitiveDelegate(PrimitiveToFormat));
            Primitives.Add("sub", new PrimitiveDelegate(PrimitiveSub));
            Primitives.Add("->num", new PrimitiveDelegate(PrimitiveToNumber));
            Primitives.Add("->str", new PrimitiveDelegate(PrimitiveToString));

            Primitives.Add("EVENT", new PrimitiveDelegate(PrimitiveEvent));
            Primitives.Add("event.fire", new PrimitiveDelegate(PrimitiveEventFire));
            Primitives.Add("event.purge", new PrimitiveDelegate(PrimitiveEventPurge));

            Primitives.Add("AFTER", new PrimitiveDelegate(PrimitiveTimerAfter));
            Primitives.Add("EVERY", new PrimitiveDelegate(PrimitiveTimerEvery));
            Primitives.Add("timer.start", new PrimitiveDelegate(PrimitiveTimerStart));
            Primitives.Add("timer.stop", new PrimitiveDelegate(PrimitiveTimerStop));
            Primitives.Add("timer.purge", new PrimitiveDelegate(PrimitiveTimerPurge));

            Primitives.Add("skills", new PrimitiveDelegate(PrimitiveGetSkills));
            Primitives.Add("hasSkill", new PrimitiveDelegate(PrimitiveHasSkill));

            Primitives.Add("flag.set", new PrimitiveDelegate(PrimitiveFlagSet));
            Primitives.Add("flag.clear", new PrimitiveDelegate(PrimitiveFlagClear));
            Primitives.Add("flag.isSet", new PrimitiveDelegate(PrimitiveFlagIsSet));
            Primitives.Add("flag.isClear", new PrimitiveDelegate(PrimitiveFlagIsClear));

            Primitives.Add("TASK.DEF", new PrimitiveDelegate(PrimitiveTaskDef));
            Primitives.Add("task.list", new PrimitiveDelegate(PrimitiveTaskList));
            Primitives.Add("TASK.START", new PrimitiveDelegate(PrimitiveTaskStartWithParameter));
            Primitives.Add("task.isRunning", new PrimitiveDelegate(PrimitiveTaskIsRunning));
            Primitives.Add("task.start", new PrimitiveDelegate(PrimitiveTaskStartWithoutParameter));
            Primitives.Add("task.stop", new PrimitiveDelegate(PrimitiveTaskStop));
            Primitives.Add("task.purge", new PrimitiveDelegate(PrimitiveTaskPurge));
            Primitives.Add("task.publish", new PrimitiveDelegate(PrimitiveTaskPublish));
            Primitives.Add("task.send", new PrimitiveDelegate(PrimitiveTaskSend));
            Primitives.Add("task.setResult", new PrimitiveDelegate(PrimitiveTaskSetResult));
            Primitives.Add("task.result", new PrimitiveDelegate(PrimitiveTaskGetResult));
            Primitives.Add("task.name", new PrimitiveDelegate(PrimitiveTaskGetName));
            Primitives.Add("task.wait", new PrimitiveDelegate(PrimitiveTaskWait));
            Primitives.Add("task.join", new PrimitiveDelegate(PrimitiveTaskJoin));

            Primitives.Add("debug.write", new PrimitiveDelegate(PrimitiveDebugWrite));

            Primitives.Add("error.last", new PrimitiveDelegate(PrimitiveErrorLast));    
            Primitives.Add("error.reset", new PrimitiveDelegate(PrimitiveErrorReset));
            Primitives.Add("error.throw", new PrimitiveDelegate(PrimitiveErrorThrow));

            Primitives.Add("mogwai.halt", new PrimitiveDelegate(PrimitiveHalt));
            Primitives.Add("mogwai.memory", new PrimitiveDelegate(PrimitiveGetMemory));
            Primitives.Add("mogwai.reset", new PrimitiveDelegate(PrimitiveMogwaiReset));
            Primitives.Add("mogwai.sendMessage", new PrimitiveDelegate(PrimitiveSendMessageToStudio));
            Primitives.Add("mogwai.reboot", new PrimitiveDelegate(PrimitiveMogwaiReboot));
            Primitives.Add("mogwai.info", new PrimitiveDelegate(PrimitiveMogwaiInfo));
            Primitives.Add("mogwai.frugalMode", new PrimitiveDelegate(PrimitiveMogwaiFrugalMode));
            Primitives.Add("mogwai.units", new PrimitiveDelegate(PrimitiveGetUnits));
            Primitives.Add("mogwai.units.run", new PrimitiveDelegate(PrimitiveRunUnit));
            Primitives.Add("mogwai.isTask", new PrimitiveDelegate(PrimitiveIsTask));    

            Primitives.Add("gpio.setMode.input", new PrimitiveDelegate(PrimitiveGpioModeInput));
            Primitives.Add("gpio.setMode.inputPullDown", new PrimitiveDelegate(PrimitiveGpioSetModeInputPullDown));
            Primitives.Add("gpio.setMode.inputPullUp", new PrimitiveDelegate(PrimitiveGpioSetModeInputPullUp));
            Primitives.Add("gpio.setMode.output", new PrimitiveDelegate(PrimitiveGpioSetModeOutput));
            Primitives.Add("gpio.read", new PrimitiveDelegate(PrimitiveGpioPinRead));
            Primitives.Add("gpio.write.high", new PrimitiveDelegate(PrimitiveGpioPinWriteHigh));
            Primitives.Add("gpio.write.low", new PrimitiveDelegate(PrimitiveGpioPinWriteLow));
            Primitives.Add("gpio.toggle", new PrimitiveDelegate(PrimitiveGpioPinToggle));
            Primitives.Add("gpio.close", new PrimitiveDelegate(PrimitiveGpioPinClose));

            Primitives.Add("i2c.open", new PrimitiveDelegate(PrimitiveI2cOpen));
            Primitives.Add("i2c.close", new PrimitiveDelegate(PrimitiveI2cClose));
            Primitives.Add("i2c.write", new PrimitiveDelegate(PrimitiveI2cWrite));
            Primitives.Add("i2c.register.write", new PrimitiveDelegate(PrimitiveI2cRegisterWrite));
            Primitives.Add("i2c.read", new PrimitiveDelegate(PrimitiveI2cRead));
            Primitives.Add("i2c.register.read", new PrimitiveDelegate(PrimitiveI2cRegisterRead));
            Primitives.Add("i2c.scan", new PrimitiveDelegate(PrimitiveI2cScan));

            Primitives.Add("ssd1306.init", new PrimitiveDelegate(PrimitiveSsd1306Init));
            Primitives.Add("ssd1306.close", new PrimitiveDelegate(PrimitiveSsd1306Close));
            Primitives.Add("ssd1306.clear", new PrimitiveDelegate(PrimitiveSsd1306Clear));
            Primitives.Add("ssd1306.printString", new PrimitiveDelegate(PrimitiveSsd1306PrintString));
            Primitives.Add("ssd1306.drawString", new PrimitiveDelegate(PrimitiveSsd1306DrawString));
            Primitives.Add("ssd1306.refresh", new PrimitiveDelegate(PrimitiveSsd1306Refresh));
            Primitives.Add("ssd1306.drawPixel", new PrimitiveDelegate(PrimitiveSsd1306DrawPixel));
            Primitives.Add("ssd1306.drawHorizontalLine", new PrimitiveDelegate(PrimitiveSsd1306DrawHorizontalLine));
            Primitives.Add("ssd1306.drawVerticalLine", new PrimitiveDelegate(PrimitiveSsd1306DrawHVerticalLine));
            Primitives.Add("ssd1306.drawRectangle", new PrimitiveDelegate(PrimitiveSsd1306DrawRectangle));
            Primitives.Add("ssd1306.drawFilledRectangle", new PrimitiveDelegate(PrimitiveSsd1306DrawFilledRectangle));
            Primitives.Add("ssd1306.drawBitmap", new PrimitiveDelegate(PrimitiveSsd1306DrawBitmap));

            Primitives.Add("pwm.open", new PrimitiveDelegate(PrimitivePwmOpen));
            Primitives.Add("pwm.close", new PrimitiveDelegate(PrimitivePwmClose));
            Primitives.Add("pwm.start", new PrimitiveDelegate(PrimitivePwmStart));
            Primitives.Add("pwm.stop", new PrimitiveDelegate(PrimitivePwmStop));

            Primitives.Add("adc.open", new PrimitiveDelegate(PrimitiveAdcOpen));
            Primitives.Add("adc.close", new PrimitiveDelegate(PrimitiveAdcClose));
            Primitives.Add("adc.read", new PrimitiveDelegate(PrimitiveAdcReadValue));
            Primitives.Add("adc.resolutionInBits", new PrimitiveDelegate(PrimitiveAdcGetResolutionInBits));
            Primitives.Add("adc.maxValue", new PrimitiveDelegate(PrimitiveAdcGetMaxValue));

            Primitives.Add("device.setPinFunction", new PrimitiveDelegate(PrimitiveDeviceSetPinFunction));

            Primitives.Add("stopwatch.create", new PrimitiveDelegate(PrimitiveStopwatchCreate));   
            Primitives.Add("stopwatch.start", new PrimitiveDelegate(PrimitiveStopwatchStart));
            Primitives.Add("stopwatch.stop", new PrimitiveDelegate(PrimitiveStopwatchStop));
            Primitives.Add("stopwatch.reset", new PrimitiveDelegate(PrimitiveStopwatchReset));
            Primitives.Add("stopwatch.restart", new PrimitiveDelegate(PrimitiveStopwatchRestart));
            Primitives.Add("stopwatch.isRunning", new PrimitiveDelegate(PrimitiveStopwatchIsRunning));
            Primitives.Add("stopwatch.elapsed", new PrimitiveDelegate(PrimitiveStopwatchElapsed));
            Primitives.Add("stopwatch.purge", new PrimitiveDelegate(PrimitiveStopwatchPurge));

            Primitives.Add("STO", new PrimitiveDelegate(PrimitiveSto));
            Primitives.Add("REPEAT", new PrimitiveDelegate(PrimitiveRepeat));
            Primitives.Add("IF", new PrimitiveDelegate(PrimitiveIf));
            Primitives.Add("IFELSE", new PrimitiveDelegate(PrimitiveIfElse));
            Primitives.Add("WHILE", new PrimitiveDelegate(PrimitiveWhile));
            Primitives.Add("FOR", new PrimitiveDelegate(PrimitiveFor));
            Primitives.Add("FORSTEP", new PrimitiveDelegate(PrimitiveForStep));
            Primitives.Add("FOREVER", new PrimitiveDelegate(PrimitiveForever));
            Primitives.Add("DEFUNC", new PrimitiveDelegate(PrimitiveDefunc));
            Primitives.Add("FOREACH", new PrimitiveDelegate(PrimitiveForeach));
            Primitives.Add("TRAP", new PrimitiveDelegate(PrimitiveTrap));
            Primitives.Add("GUARD", new PrimitiveDelegate(PrimitiveGuard));
            Primitives.Add("DURING", new PrimitiveDelegate(PrimitiveDuring));    
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
                if (Primitives.Contains(name))
                    return false;
            }

            return name.IndexOfAny(_invalidChars) == -1;
        }

        public EvalResult Run(string code, bool debugMode = false)
        {
            if (IsTask)
            {
                Debug.WriteLine($"task   '{Name}' run with {GC.Run(true)} bytes free");
            }
            else
            {
                Debug.WriteLine($"mother '{Name}' run with {GC.Run(true)} bytes free");
            }

            try
            {
                IsRunning = true;

                Reset();

                // _debugMode = debugMode

                var stopwatch = Stopwatch.StartNew();

                if (IsTask)
                    MotherEngine.FireEvent(MOGTask.EVENT_TASK_DID_START, new MOGName(MotherEngine, Name));

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

                    if (IsTask)
                    {
                        var failureInformations = new MOGRecord(MotherEngine);
                        failureInformations.SetItem("task", new MOGName(MotherEngine, Name));
                        failureInformations.SetItem("error", new MOGString(MotherEngine, LastResult.Error.Code));
                        failureInformations.SetItem("message", new MOGString(MotherEngine, LastResult.Error.Message));

                        MotherEngine.FireEvent(MOGTask.EVENT_TASK_DID_FAIL, failureInformations);
                    }

                    if (Delegate != null)
                        Delegate.ProgramEnd(this, LastResult);

                    return LastResult;
                }

                HaltRequested = false;
                //ExitRequested = false;
                //ReturnRequested = false;

                EvalResult result = program.Execute();

                program = null;

                HaltRequested = false;
                //ExitRequested = false;
                //ReturnRequested = false;

                EvalResult result2;

                if (result.IsError)
                {
                    if (Functions.Contains("MOGWAI.onError"))
                    {
                        var onErrorFunction = Functions["MOGWAI.onError"] as MOGFunction;
                        result2 = onErrorFunction.Execute();

                        if (result2.IsError)
                            result = result2;
                    }
                }
                else
                {
                    if (Functions.Contains("MOGWAI.onStop"))
                    {
                        var onStopFunction = Functions["MOGWAI.onStop"] as MOGFunction;
                        result2 = onStopFunction.Execute();

                        if (result2.IsError)
                            result = result2;
                    }
                }

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                // _debugMode = false;

                LastResult = result;

                if (Delegate != null)
                    Delegate.ProgramEnd(this, result);

                if (LastResult != EvalResult.NoError)
                {
                    if (IsTask)
                    {
                        var failureInformations = new MOGRecord(MotherEngine);
                        failureInformations.SetItem("task", new MOGName(MotherEngine, Name));
                        failureInformations.SetItem("error", new MOGString(MotherEngine, LastResult.Error.Code));
                        failureInformations.SetItem("message", new MOGString(MotherEngine, LastResult.Error.Message));

                        MotherEngine.FireEvent(MOGTask.EVENT_TASK_DID_FAIL, failureInformations);
                    }
                }
                else
                {
                    if (IsTask)
                    {
                        var endInformations = new MOGRecord(MotherEngine);
                        endInformations.SetItem("task", new MOGName(MotherEngine, Name));
                        endInformations.SetItem("result",TaskResult);

                        MotherEngine.FireEvent(MOGTask.EVENT_TASK_DID_END, endInformations);
                    }
                }

                return LastResult;
            }
            finally
            {
                Reset();
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
            CurrentEvalObject = null;

            CleanupTasks();

            CleanupStopwatchs();

            CleanupTimers();

            CleanupEvents();

            CleanupWaitingFireObjects();

            CleanupOpenPins();

            CleanupI2cDevices();

            CleanupPwmChannels();

            CleanupAdcChannels();

            if (Ssd1306 != null)
            {
                Ssd1306.Dispose();
                Ssd1306 = null;
            }

            _stacks.Clear();
            _currentStack = new MOGStack();
            _stacks.Add(_currentStack);

            var glb = _varsContext[0] as VarContext;
            glb.Clear();

            Functions.Clear();

            DisableInterrupts = false;

            HaltRequested = false;
            BreakRequested = false;

            FrugalMode = false;
        }

        public void Halt() => HaltRequested = true;

        public string[] Units
        {
            get
            {
                try
                {
                    var unitsFolder = @"I:\mogwai\units";
                    var unitFiles = Directory.GetFiles(unitsFolder);

                    var unitNames = new string[unitFiles.Length];

                    for (int i = 0; i < unitFiles.Length; i++)
                    {
                        var fileName = Path.GetFileName(unitFiles[i]);
                        unitNames[i] = fileName;
                    }

                    return unitNames;
                }
                catch
                {
                    return new string[0];
                }
            }
        }

        #region PRIMITIVES

        private static EvalResult PrimitivePlus(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // 15 20 +

                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                n0.Value += n1.Value;

                engine.StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // "AA" "BB" +

                var s1 = engine.StackPop() as MOGString;
                var s0 = engine.StackPop() as MOGString;

                s0.Value += s1.Value;

                engine.StackPush(s0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGString))
            {
                // "BB" 456 +

                var s1 = engine.StackPop() as MOGNumber;
                var s0 = engine.StackPop() as MOGString;

                s0.Value += s1.Value.ToString();

                engine.StackPush(s0);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGNumber))
            {
                // 456 "BB" +

                var s1 = engine.StackPop() as MOGString;
                var s0 = engine.StackPop() as MOGNumber;

                s1.Value = s0.Value.ToString() + s1.Value;

                engine.StackPush(s1);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGList))
            {
                // (1 2 3) "TOTO" + 

                var item = engine.StackPop();

                var list = engine.StackPop() as MOGList;
                list.AddItem(item);

                engine.StackPush(list);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGData) && s[0] == typeof(MOGNumber))
            {
                // D:FFAB45 123 +

                var value = engine.StackPop() as MOGNumber;
                var data = engine.StackPop() as MOGData;

                data.AddItem((byte)value.Value);

                engine. StackPush(data);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, @ref.ToString());

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitivePlus(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMathSubstraction(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                n0.Value -= n1.Value;

                engine.StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, @ref.ToString());

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitiveMathSubstraction(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMathMultiplication(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                n0.Value *= n1.Value;

                engine.StackPush(n0);
                
                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, @ref.ToString());

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitiveMathMultiplication(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMathDivision(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                n0.Value /= n1.Value;

                engine.StackPush(n0);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X +

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, @ref.ToString());

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitiveMathDivision(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMathFloor(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var number = engine.StackPop() as MOGNumber;

            try
            {
                var n = new MOGNumber(engine, (float)Math.Floor((double)number.Value));
                engine.StackPush(n);

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.MathematicalError, name, ex.Message);
            }
        }

        private static EvalResult PrimitiveMathModulo(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var n1 = engine.StackPop() as MOGNumber;

            try
            {
                var v = n1.Value % n0.Value;
                engine.StackPush(new MOGNumber(engine, v));

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.MathematicalError, name, ex.Message);
            }
        }

        private static EvalResult PrimitiveGetType(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize < 1)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var n0 = engine.StackPop();
            engine.StackPush(n0.Type.Clone());

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveEval(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var obj = engine.StackPop();
            return obj.UserEval();
        }

        private static EvalResult PrimitiveStackClear(MogwaiNanoEngine engine, string name)
        {
            engine.StackClear();
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveStackSwap(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize < 2)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            engine.StackSwap();
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveStackDup(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize < 1)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            engine.StackDup();
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveStackDrop(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            engine.StackDrop();
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveDebugWrite(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var n0 = engine.StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(value);

                var r = PrimitiveDebugWrite(engine, name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (engine.Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return engine.Delegate.DebugMessage(engine, @string.Value);
                }
                else
                {
                    return  engine.Delegate.DebugMessage(engine, n0.ToString());
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveConsolePrintLn(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var n0 = engine.StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(value);

                var r = PrimitiveConsolePrintLn(engine, name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (engine.Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return engine.Delegate.ConsolePrintLn(engine, @string.Value);
                }
                else
                {
                    return engine.Delegate.ConsolePrintLn(engine, n0.ToString());
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveConsolePrint(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var n0 = engine.StackPop();

            if (n0 is MOGRef @ref)
            {
                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(value);

                var r = PrimitiveConsolePrint(engine, name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            if (engine.Delegate != null)
            {
                if (n0 is MOGString @string)
                {
                    return engine.Delegate.ConsolePrint(engine, @string.Value);
                }
                else
                {
                    return engine.Delegate.ConsolePrint(engine, n0.ToString());
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveSto(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n = engine.StackPop() as MOGName;
            var value = engine.StackPop() as MOGObject;

            if (!engine.IsValidName(n.Value, true))
                return EvalResult.Failure(engine, Error.InvalidNameError, name, n.ToString());

            return engine.VarWrite(n.Value, value);
        }

        private static EvalResult PrimitiveBreak(MogwaiNanoEngine engine, string name)
        {
            engine.BreakRequested = true;
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveHalt(MogwaiNanoEngine engine, string name)
        {
            engine.HaltRequested = true;
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveWait(MogwaiNanoEngine engine, string name)
        {
            // 50 wait

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var v = engine.StackPop() as MOGNumber;

            if (v.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalMilliseconds <= v.Value)
            {
                Thread.Sleep(0);

                var result = engine.ExecuteWaitingFireObjects();

                if (result != EvalResult.NoError)
                    return result;
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveGet(MogwaiNanoEngine engine, string name)
        {
            // (1 2 3) 0 get
            // [x: 50 y: 10] x: get

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGList))
            {
                // (1 2 3) 0 get

                var index = engine.StackPop() as MOGNumber;
                var list = engine.StackPop() as MOGList;

                if (index.Value < 0 || index.Value >= list.Items.Count)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                var item = list.GetItem((int)index.Value);
                engine.StackPush(item);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGKey) && s[1] == typeof(MOGRecord))
            {
                // [x: 10 y: 20] x: get

                var key = engine.StackPop() as MOGKey;
                var record = engine.StackPop() as MOGRecord;

                var item = record.GetItem(key.Value);

                if (item == null)
                {
                    engine.StackPush(new MOGNull(engine));
                }
                else
                {
                    engine.StackPush(item);
                }

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData))
            {
                // D:FFAA45 0 get

                var index = engine.StackPop() as MOGNumber;
                var data = engine.StackPop() as MOGData;

                if (index.Value < 0 || index.Value >= data.Items.Length)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                var item = data.GetItem((int)index.Value);
                engine.StackPush(new MOGNumber(engine, item));

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // &ref X get

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, @ref.Value);

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitiveGet(engine, name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSet(MogwaiNanoEngine engine, string name)
        {
            // 10 (1 2 3) 0 set ---> (10 2 3)
            // 100 [x: 10 y: 20] x: set ---> [x: 100 y: 20]

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGList))
            {
                // 10 (1 2 3) 0 set

                var index = engine.StackPop() as MOGNumber;
                var list = engine.StackPop() as MOGList;
                var value = engine.StackPop();

                if (index.Value < 0 || index.Value >= list.Items.Count)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                list.SetItem((int)index.Value, value);
                engine.StackPush(list);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGKey) && s[1] == typeof(MOGRecord))
            {
                // 100 [x: 10 y: 20] x: set

                var key = engine.StackPop() as MOGKey;
                var record = engine.StackPop() as MOGRecord;
                var value = engine.StackPop();

                record.SetItem(key.Value, value);
                engine.StackPush(record);

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData) && s[2] == typeof(MOGNumber))
            {
                // 10 D:FFAA45 0 set

                var index = engine.StackPop() as MOGNumber;
                var data = engine.StackPop() as MOGData;
                var value = engine.StackPop() as MOGNumber;

                if (index.Value < 0 || index.Value >= data.Items.Length)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                data.SetItem((int)index.Value, (byte)value.Value);
                engine.StackPush(data);

                return EvalResult.NoError;
            }
            else if (s[1] == typeof(MOGRef))
            {
                // 10 &X 5 set
                // 10 &X x: set

                var item = engine.StackPop();
                var @ref = engine.StackPop() as MOGRef;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                engine.StackPush(value);
                engine.StackPush(item);

                var r = PrimitiveSet(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSize(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGList))
            {
                var list = engine.StackPop() as MOGList;
                engine.StackPush(new MOGNumber(engine, list.Items.Count));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;
                engine.StackPush(new MOGNumber(engine, record.Items.Count));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString))
            {
                var @string = engine.StackPop() as MOGString;
                engine.StackPush(new MOGNumber(engine, @string.Value.Length));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGData))
            {
                var data = engine.StackPop() as MOGData;
                engine.StackPush(new MOGNumber(engine, data.Items.Length));
                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRef))
            {
                var @ref = engine.StackPop() as MOGRef;
                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(value);

                var r = PrimitiveSize(engine, name);

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePurge(MogwaiNanoEngine engine, string name)
        {
            // 'A' purge

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var varName = engine.StackPop() as MOGName;

                if (engine.VarPurge(varName.Value))
                    return EvalResult.NoError;

                return EvalResult.Failure(engine, Error.UnknownNameError, name, varName.Value);
            }
            else if (s[0] == typeof(MOGKey))
            {
                s = engine.StackSign(2);

                if (s.Length == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

                if (s[0] == typeof(MOGKey) && s[1] == typeof(MOGRecord))
                {
                    var key = engine.StackPop() as MOGKey;
                    var record = engine.StackPop() as MOGRecord;

                    if (record.RemoveItem(key.Value))
                    {
                        engine.StackPush(record);
                        return EvalResult.NoError;
                    }

                    return EvalResult.Failure(engine, Error.UnknownKeyError, name, key.Value);
                }
                else if (s[1] == typeof(MOGRef))
                {
                    var n0 = engine.StackPop();
                    var reference = engine.StackPop() as MOGRef;
                    var value = engine.VarRead(reference.Value, false);

                    if (value == null)
                        return EvalResult.Failure(engine, Error.UnknownNameError, name, reference.ToString());

                    // Le contenu de la variable doit être de type record

                    if (value is MOGRecord)
                    {
                        engine.StackPush(value);
                        engine.StackPush(n0);

                        var r = PrimitivePurge(engine, name);

                        if (r.IsError)
                            return r;

                        // On enlève la valeur modifiée de la stack qui ne sert à rien

                        engine.StackDrop();

                        return EvalResult.NoError;
                    }
                    else
                    {
                        return EvalResult.Failure(engine, Error.BadArgumentTypeError, name, reference.ToString(), $"var type .{value.Type.Value} not allowed");
                    }
                }
            }
            else if (s[0] == typeof(MOGNumber))
            {
                s = engine.StackSign(2);

                if (s.Length == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

                if (s[1] == typeof(MOGList))
                {
                    var index = engine.StackPop() as MOGNumber;
                    var list = engine.StackPop() as MOGList;

                    var result = list.RemoveItem((int)index.Value);

                    if (result == EvalResult.NoError)
                        engine.StackPush(list);

                    return result;
                }
                else if (s[1] == typeof(MOGData))
                {
                    var index = engine.StackPop() as MOGNumber;
                    var data = engine.StackPop() as MOGData;

                    var result = data.RemoveItem((int)index.Value);

                    if (result == EvalResult.NoError)
                        engine.StackPush(data);

                    return result;
                }
                else if (s[1] == typeof(MOGRef))
                {
                    var n0 = engine.StackPop();

                    var reference = engine.StackPop() as MOGRef;
                    var value = engine.VarRead(reference.Value, false);

                    if (value == null)
                        return EvalResult.Failure(engine, Error.UnknownNameError, name, reference.ToString());

                    // Le contenu de la variable doit être de type list ou data

                    if (value is MOGList || value is MOGData)
                    {
                        engine.StackPush(value);
                        engine.StackPush(n0);

                        var r = PrimitivePurge(engine, name);

                        if (r.IsError)
                            return r;

                        // On enlève la valeur modifiée de la stack qui ne sert à rien

                        engine.StackDrop();

                        return EvalResult.NoError;
                    }
                    else
                    {
                        return EvalResult.Failure(engine, Error.BadArgumentTypeError, name, reference.ToString(), $"var type .{value.Type.Value} not allowed");
                    }
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

        }

        private static EvalResult PrimitiveExists(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGName))
            {
                var varName = engine.StackPop() as MOGName;
                var exists = engine.VarExists(varName.Value);
               
                engine.StackPush(new MOGBoolean(engine, exists));
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveDI(MogwaiNanoEngine engine, string name)
        {
            engine.DisableInterrupts = true;
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveEI(MogwaiNanoEngine engine, string name)
        {
            engine.DisableInterrupts = false;
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveToData(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var n0 = engine.StackPop() as MOGNumber;
                var size = (int)n0.Value;

                if (size > engine.StackSize)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

                var types = engine.StackSign(size);

                for (int i = 0; i < size; i++)
                {
                    if (types[i] != typeof(MOGNumber))
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "only numbers between 0 and 255 are allowed.");
                }

                var items = new byte[size];

                for (int i = size - 1; i >= 0; i--)
                {
                    var n = engine.StackPop() as MOGNumber;
                    items[i] = (byte)n.Value;
                }

                var data = new MOGData(engine, items);
                engine.StackPush(data);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMakeData(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var initValue = engine.StackPop() as MOGNumber;
                var size = engine.StackPop() as MOGNumber;

                var items = new byte[(int)size.Value];
                var value = (byte)initValue.Value;

                if (value != 0)
                {
                    for (int i = 0; i < items.Length; i++)
                        items[i] = value;
                }

                var data = new MOGData(engine, items);

                engine.StackPush(data);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionEqual(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // n1 n2 ==

                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value == n1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGType) && s[1] == typeof(MOGType))
            {
                // t1 t2 ==

                var t1 = engine.StackPop() as MOGType;
                var t0 = engine.StackPop() as MOGType;

                engine.StackPush(new MOGBoolean(engine, t0.Value == t1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // s1 s2 ==

                var s1 = engine.StackPop() as MOGString;
                var s0 = engine.StackPop() as MOGString;

                engine.StackPush(new MOGBoolean(engine, s0.Value == s1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGName) && s[1] == typeof(MOGName))
            {
                // 's1' 's2' ==

                var s1 = engine.StackPop() as MOGName;
                var s0 = engine.StackPop() as MOGName;

                engine.StackPush(new MOGBoolean(engine, s0.Value == s1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionNotEqual(MogwaiNanoEngine engine, string name)
        {
            // v1 v2 !=

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                // n1 n2 != 

                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value != n1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGType) && s[1] == typeof(MOGType))
            {
                // t1 t2 !=

                var t1 = engine.StackPop() as MOGType;
                var t0 = engine.StackPop() as MOGType;

                engine.StackPush(new MOGBoolean(engine, t0.Value != t1.Value));

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGString) && s[1] == typeof(MOGString))
            {
                // s1 s2 !=

                var t1 = engine.StackPop() as MOGString;
                var t0 = engine.StackPop() as MOGString;

                engine.StackPush(new MOGBoolean(engine, t0.Value != t1.Value));

                return EvalResult.NoError;
            }

            else if (s[0] == typeof(MOGName) && s[1] == typeof(MOGName))
            {
                // 's1' 's2' !=

                var t1 = engine.StackPop() as MOGName;
                var t0 = engine.StackPop() as MOGName;

                engine.StackPush(new MOGBoolean(engine, t0.Value != t1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionInferior(MogwaiNanoEngine engine, string name)
        {
            // v1 v2 <

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value < n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionInferiorOrEqual(MogwaiNanoEngine engine, string name)
        {
            // v1 v2 <=

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value <= n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionSuperior(MogwaiNanoEngine engine, string name)
        {
            // v1 v2 >

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value > n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionSuperiorOrEqual(MogwaiNanoEngine engine, string name)
        {
            // v1 v2 >=

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n1 = engine.StackPop() as MOGNumber;
                var n0 = engine.StackPop() as MOGNumber;

                engine.StackPush(new MOGBoolean(engine, n0.Value >= n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionIsNull(MogwaiNanoEngine engine, string name)
        {
            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var n0 = engine.StackPop();
            var b = n0 is MOGNull;

            engine.StackPush(new MOGBoolean(engine, b));
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveConditionAnd(MogwaiNanoEngine engine, string name)
        {
            // true false and

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = engine.StackPop() as MOGBoolean;
                var n0 = engine.StackPop() as MOGBoolean;

                engine.StackPush(new MOGBoolean(engine, n0.Value && n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionOr(MogwaiNanoEngine engine, string name)
        {
            // true false or

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = engine.StackPop() as MOGBoolean;
                var n0 = engine.StackPop() as MOGBoolean;

                engine.StackPush(new MOGBoolean(engine, n0.Value || n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveConditionXor(MogwaiNanoEngine engine, string name)
        {
            // true false xor

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGBoolean))
            {
                var n1 = engine.StackPop() as MOGBoolean;
                var n0 = engine.StackPop() as MOGBoolean;

                engine.StackPush(new MOGBoolean(engine, n0.Value ^ n1.Value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveNot(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGBoolean))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var b = engine.StackPop() as MOGBoolean;
            b.Value = !b.Value;
            engine.StackPush(b);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveRepeat(MogwaiNanoEngine engine, string name)
        {
            // 5 {...} REPEAT

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[1] != typeof(MOGNumber) || s[0] != typeof(MOGCode))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var code = engine.StackPop() as MOGCode;
            var n = engine.StackPop() as MOGNumber;

            for (int i = 0; i < n.Value; i++)
            {
                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (engine.BreakRequested)
                {
                    engine.BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveIf(MogwaiNanoEngine engine, string name)
        {
            try
            {
                // true {...} IF

                var s = engine.StackSign(2);

                if (s.Length == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

                if (s[1] != typeof(MOGBoolean) || s[0] != typeof(MOGCode))
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

                var code = engine.StackPop() as MOGCode;
                var condition = engine.StackPop() as MOGBoolean;

                if (condition.Value)
                    return code.Execute();

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.FatalError, name, ex.Message);
            }
        }

        private static EvalResult PrimitiveIfElse(MogwaiNanoEngine engine, string name)
        {
            try
            {
                // true {...} {...} IFELSE

                var s = engine.StackSign(3);

                if (s.Length == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

                if (s[2] != typeof(MOGBoolean) || s[1] != typeof(MOGCode) || s[0] != typeof(MOGCode))
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

                var code1 = engine.StackPop() as MOGCode;
                var code2 = engine.StackPop() as MOGCode;
                var condition = engine.StackPop() as MOGBoolean;

                if (condition.Value)
                    return code2.Execute();

                return code1.Execute();
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.FatalError, name, ex.Message);
            }
        }

        private static EvalResult PrimitiveWhile(MogwaiNanoEngine engine, string name)
        {
            // { condition } { code } WHILE

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGCode) || s[1] != typeof(MOGCode))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var code = engine.StackPop() as MOGCode;
            var conditionCode = engine.StackPop() as MOGCode;

            while (true)
            {
                var conditionResult = conditionCode.Execute();

                if (conditionResult.IsError)
                    return conditionResult;

                if (engine.BreakRequested)
                {
                    engine.BreakRequested = false;
                    break;
                }

                var conditionValue = engine.StackPop() as MOGBoolean;

                if (conditionValue == null)
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

                if (!conditionValue.Value)
                    break;

                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (engine.BreakRequested)
                {
                    engine.BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveFor(MogwaiNanoEngine engine, string name)
        {
            // 1 2 'i' {...} FOR

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var code = engine.StackPop() as MOGCode;
                var varName = engine.StackPop() as MOGName;
                var end = engine.StackPop() as MOGNumber;
                var start = engine.StackPop() as MOGNumber;

                var direction = (end!.Value - start!.Value) > 0 ? 1 : -1;
                var varLoop = new MOGNumber(engine, 0);

                EvalResult result = EvalResult.NoError;

                for (float i = start.Value; direction > 0 ? i <= end.Value : i >= end.Value; i += direction)
                {
                    if (engine.BreakRequested)
                    {
                        engine.BreakRequested = false;
                        break;
                    }

                    varLoop.Value = i;
                    result = engine.VarWrite(varName.Value, varLoop);

                    if (result != EvalResult.NoError)
                        break;

                    result = code.Execute();

                    if (result != EvalResult.NoError)
                        break;
                }

                return result;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveForStep(MogwaiNanoEngine engine, string name)
        {
            // 1 2 2 'i' {...} FORSTEP

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var code = engine.StackPop() as MOGCode;
                var varName = engine.StackPop() as MOGName;
                var step = engine.StackPop() as MOGNumber;
                var end = engine.StackPop() as MOGNumber;
                var start = engine.StackPop() as MOGNumber;

                var direction = (end.Value - start.Value) > 0 ? 1 : -1;
                step.Value = Math.Abs(step.Value) * direction;
                
                var varLoop = new MOGNumber(engine, 0);

                EvalResult result = EvalResult.NoError;

                for (float i = start.Value; direction > 0 ? i <= end.Value : i >= end.Value; i += step.Value)
                {
                    if (engine.BreakRequested)
                    {
                        engine.BreakRequested = false;
                        break;
                    }

                    varLoop.Value = i;
                    result = engine.VarWrite(varName.Value, varLoop);

                    if (result != EvalResult.NoError)
                        break;

                    result = code.Execute();

                    if (result != EvalResult.NoError)
                        break;
                }

                return result;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveForever(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGCode))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var code = engine.StackPop() as MOGCode;

            while (true)
            {
                var result = code.Execute();

                if (result.IsError)
                    return result;

                if (engine.BreakRequested)
                {
                    engine.BreakRequested = false;
                    break;
                }
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveForeach(MogwaiNanoEngine engine, string name)
        {
            // List name code FOREACH
            // (1 2 3) 'i' { i ? } FOREACH      
            // D:010203 'i' { i ? } FOREACH 
            // "XXXX" 'i' { i ? } FOREACH

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            // 0 code
            // 1 variable
            // 2 list base

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGName))
            {
                if (s[2] == typeof(MOGList))
                {
                    var code = engine.StackPop() as MOGCode;
                    var varName = engine.StackPop() as MOGName;
                    var list = engine.StackPop() as MOGList;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in list.Items)
                    {
                        if (engine.BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = engine.VarWrite(varName.Value, item as MOGObject);

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
                    var code = engine.StackPop() as MOGCode;
                    var varName = engine.StackPop() as MOGName;
                    var data = engine.StackPop() as MOGData;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in data.Items)
                    {
                        if (engine.BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = engine.VarWrite(varName.Value, new MOGNumber(engine, (byte)item));

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
                    var code = engine.StackPop() as MOGCode;
                    var varName = engine.StackPop() as MOGName;
                    var @string = engine.StackPop() as MOGString;

                    EvalResult result = EvalResult.NoError;

                    foreach (var item in @string.Value)
                    {
                        if (engine.BreakRequested) // || Engine.ExitRequested || Engine.ReturnRequested)
                            break;

                        result = engine.VarWrite(varName.Value, new MOGString(engine, item.ToString()));

                        if (result != EvalResult.NoError)
                            break;

                        result = code.Execute();

                        if (result != EvalResult.NoError)
                            break;
                    }

                    return result;
                }

            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTrap(MogwaiNanoEngine engine, string name)
        {
            // code TRAP

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode))
            {
                var code = engine.StackPop() as MOGCode;    
                var result = code.Execute();

                if (result != EvalResult.NoError)
                    engine.LastError = result.Error;

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveGuard(MogwaiNanoEngine engine, string name)
        {
            // code elseCode GUARD

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGCode))
            {
                var errorCode = engine.StackPop() as MOGCode;
                var code = engine.StackPop() as MOGCode;

                var result = code.Execute();

                if (result != EvalResult.NoError)
                {
                    engine.LastError = result.Error;

                    return errorCode.Execute();
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveDuring(MogwaiNanoEngine engine, string name)
        {
            // number {code} DURING

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGCode) && s[1] == typeof(MOGNumber))
            {
                var code = engine.StackPop() as MOGCode;
                var duration = engine.StackPop() as MOGNumber;

                if (duration.Value < 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                var result = EvalResult.NoError;
                var stopWatch = Stopwatch.StartNew();

                while (stopWatch.Elapsed.TotalMilliseconds < duration.Value)
                {
                    if (engine.BreakRequested) // || engine.ExitRequested || engine.ReturnRequested)
                    {
                        engine.BreakRequested = false;
                        break;
                    }

                    result = code.Execute();

                    if (result != EvalResult.NoError)
                        break;
                }

                stopWatch.Stop();

                return result;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveErrorLast(MogwaiNanoEngine engine, string name)
        {
            engine.StackPush(new MOGString(engine, engine.LastError.Code));
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveErrorReset(MogwaiNanoEngine engine, string name)
        {
            engine.LastError = Error.None;
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveErrorThrow(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString))
            {
                var errCode = engine.StackPop() as MOGString;
                var error = Error.GetError(errCode.Value);
                
                return EvalResult.Failure(engine, error);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveDefunc(MogwaiNanoEngine engine, string name)
        {
            // code name DEFUNC

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGFunction))
            {
                var fname = engine.StackPop() as MOGName;
                var func = engine.StackPop() as MOGFunction;

                if (engine.Functions.Contains(fname.Value))
                    return EvalResult.Failure(engine, Error.FunctionAlreadyExistsError, name);

                engine.Functions.Add(fname.Value, func);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveEvent(MogwaiNanoEngine engine, string name)
        {
            // function name EVENT

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) & s[1] == typeof(MOGFunction))
            {
                var eventName = engine.StackPop() as MOGName;
                var function = engine.StackPop() as MOGFunction;

                return engine.CreateNewEvent(eventName.Value, function);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveEventFire(MogwaiNanoEngine engine, string name)
        {
            // 'BTN_CLICK' data event.fire
            // 'BTN_CLICK' null event.fire
            // 'BTN_CLICK' now event.fire

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[1] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop();
            var n1 = engine.StackPop() as MOGName;

            return engine.FireEvent(n1.Value, n0);
        }

        private static EvalResult PrimitiveEventPurge(MogwaiNanoEngine engine, string name)
        {
            // 'eventName' event.purge

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var eventName = engine.StackPop() as MOGName;
                return engine.PurgeEvent(eventName.Value);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTimerEvery(MogwaiNanoEngine engine, string name)
        {
            // function interval name EVERY

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGFunction))
            {
                var timerName = engine.StackPop() as MOGName;
                var interval = engine.StackPop() as MOGNumber;
                var function = engine.StackPop() as MOGFunction;

                return engine.CreateNewTimer(timerName.Value, (int)interval.Value, true, function);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTimerAfter(MogwaiNanoEngine engine, string name)
        {
            // function interval name AFTER

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGFunction))
            {
                var timerName = engine.StackPop() as MOGName;
                var interval = engine.StackPop() as MOGNumber;
                var function = engine.StackPop() as MOGFunction;

                return engine.CreateNewTimer(timerName.Value, (int)interval.Value, false, function);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

        }

        private static EvalResult PrimitiveTimerStart(MogwaiNanoEngine engine, string name)
        {
            // name timer.start

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = engine.StackPop() as MOGName;

                if (!engine.Timers.Contains(timerName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                var timer = engine.Timers[timerName.Value] as MOGTimer;
                return timer.Start();
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTimerStop(MogwaiNanoEngine engine, string name)
        {
            // name timer.stop

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = engine.StackPop() as MOGName;

                if (!engine.Timers.Contains(timerName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                var timer = engine.Timers[timerName.Value] as MOGTimer;
                return timer.Stop();
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTimerPurge(MogwaiNanoEngine engine, string name)
        {
            // name timer.purge

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var timerName = engine.StackPop() as MOGName;
                return engine.PurgeTimer(timerName.Value);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveMogwaiReset(MogwaiNanoEngine engine, string name)
        {
            engine.Reset();
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveMogwaiReboot(MogwaiNanoEngine engine, string name)
        {
            if (engine.Functions.Contains("MOGWAI.onReboot"))
            {
                var onRebootFunction = engine.Functions["MOGWAI.onReboot"] as MOGFunction;
                var r = onRebootFunction.Execute();

                if (r.IsError)
                    return r;
            }

            Thread.Sleep(1000);

            Power.RebootDevice(5000, RebootOption.ClrOnly);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveGetMemory(MogwaiNanoEngine engine, string name)
        {
            // true or false getMemory

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var b = engine.StackPop() as MOGBoolean;
            var v = GC.Run(b.Value);

            engine.StackPush(new MOGNumber(engine, v));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveMogwaiInfo(MogwaiNanoEngine engine, string name)
        {
            var record = new MOGRecord(engine);

            record.SetItem("name", new MOGString(engine, AppGlobal.NanoParameters.Name));
            record.SetItem("mogwai", new MOGString(engine, MogwaiNanoEngine.Version.ToString()));
            record.SetItem("ip", new MOGString(engine, value: AppGlobal.IpAddress));
            record.SetItem("session", new MOGString(engine, AppGlobal.Session.ToString()));

            record.SetItem("platform", new MOGString(engine, SystemInfo.Platform));
            record.SetItem("target", new MOGString(engine, SystemInfo.TargetName));
            record.SetItem("oem", new MOGString(engine, SystemInfo.OEMString));
            record.SetItem("system", new MOGString(engine, SystemInfo.Version.ToString()));

            var memory = GC.Run(false);
            record.SetItem("memory", new MOGNumber(engine, memory));

            var skills = new MOGList(engine);

            foreach (var skill in _skills)
                skills.AddItem(new MOGString(engine, skill));

            record.SetItem("skills", skills);

            var units = new MOGList(engine);
            
            foreach (var unit in engine.Units)
                units.AddItem(new MOGString(engine, unit));

            record.SetItem("units", units); 

            record.SetItem("frugalMode", new MOGBoolean(engine, engine.FrugalMode)); 

            engine.StackPush(record);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveMogwaiFrugalMode(MogwaiNanoEngine engine, string name)
        {
            // true or false frugalMode

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGBoolean))
            {
                var b = engine.StackPop() as MOGBoolean;
                engine.FrugalMode = b.Value;
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);  
        }

        private static EvalResult PrimitiveSendMessageToStudio(MogwaiNanoEngine engine, string name)
        {
            //"MESSAGE" mogwai.sendMessage

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString))
            {
                var payload = engine.StackPop() as MOGString;
                var message = new ServerMessage(AppGlobal.NanoParameters.Name, "SEND.MESSAGE", payload.Value);
                AppGlobal.TcpServer.SendMessage(message);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveBcdToDecimal(MogwaiNanoEngine engine, string name)
        {
            // bcd->

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var number = engine.StackPop() as MOGNumber;

            if (number == null)
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            int bcdValue = (int)number.Value;
            int decimalValue = ((bcdValue >> 4) * 10) + (bcdValue & 0x0F);

            engine.StackPush(new MOGNumber(engine, decimalValue));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveDecimalToBcd(MogwaiNanoEngine engine, string name)
        {
            // ->bcd

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var number = engine.StackPop() as MOGNumber;

            if (number == null)
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            int decimalValue = (int)number.Value;

            if (decimalValue < 0 || decimalValue > 99)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name); // BCD sur un octet = 0-99

            int bcdValue = ((decimalValue / 10) << 4) | (decimalValue % 10);

            engine.StackPush(new MOGNumber(engine, bcdValue));
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveStackToVars(MogwaiNanoEngine engine, string name)
        {
            // 10 20 30 ( 'A' 'B' 'C') ->vars -----> A=10 B=20 C=30
            // [id: 50 name: "SIBUE" x: 'Z'] ->vars -------> id=50 name="SIBUE" x='Z'

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGList))
            {
                // Signature 10 20 30 ( 'A' 'B' 'C') ->vars

                var list = engine.StackPop() as MOGList;

                // La liste ne doit comporter QUE des names

                foreach (var item in list.Items)
                {
                    if (item is not MOGName)
                        return EvalResult.Failure(engine, Error.BadArgumentTypeError, name, "the list parameter can only contain names.");
                }

                // La stack doit comporter assez d'éléments

                if (engine.StackSize < list.Items.Count)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, name, "the stack does not contain enough elements.");

                // Pour chaque name on prend un item de la stack et on crée une variable avec
                // On travaille à l'envers pour que les paramètres soient dans le bon sens

                for (int i = list.Items.Count - 1; i >= 0; i--)
                {
                    var varName = list.Items[i] as MOGName;
                    var item = engine.StackPop();

                    var r2 = engine.VarWrite(varName.Value, item!);

                    if (r2 != EvalResult.NoError)
                        return r2;
                }

                return EvalResult.NoError;
            }
            else if (s[0] == typeof(MOGRecord))
            {
                // Signature [id: 50 name: "SIBUE" x: 'Z'] ->vars

                var record = engine.StackPop() as MOGRecord;

                foreach (var key in record!.Items.Keys)
                {
                    var item = record.Items[key] as MOGObject;
                    engine.VarWrite(key as string, item);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStackToSafeVars(MogwaiNanoEngine engine, string name)
        {
            // 10 "SIBUE" 'Z' [id: .number name: .string x: .name] ->safeVars -------> id=50 name="SIBUE" x='Z'

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGRecord))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            // On récupère le record de référence et ses clés

            var recf = engine.StackPop() as MOGRecord;
            var keys = new string[recf.Keys.Count];
            var i = 0;

            foreach (var k in recf.Keys)
                keys[i++] = k as string;

            // Le record de référence ne doit porter QUE des types

            foreach (var k in keys)
            {
                if (recf.Items[k] is not MOGType)
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, "reference record must have .type values.");
            }

            // La pile doit au moins contenir le nombre de clés du record de référence

            if (engine.StackSize < recf.Items.Count)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, "the stack does not contain enough elements.");

            // On récupère toute les valeurs depuis la pile

            var values = new ArrayList();

            for (i = 0; i < keys.Length; i++)
            {
                var value = engine.StackPop();
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
                    return EvalResult.Failure(engine, Error.BadArgumentTypeError, name, $"{tv} expected but {pv.Type} found for '{keys[i]}' parameter");
            }

            // On crée les variables locales

            index = 0;

            for (i = keys.Length - 1; i >= 0; i--)
            {
                var v = values[index++] as MOGObject;
                var r = engine.VarWrite(keys[i], v);

                if (r != EvalResult.NoError)
                    return r;
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveStackToParams(MogwaiNanoEngine engine, string name)
        {
            // [id: 50 name: "SIBUE" x: 'Z'] [id: .number name: .string u: (.boolean true)] ->params -------> id=50 name="SIBUE u=true"

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGRecord) || s[1] != typeof(MOGRecord))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGRecord;
            var n1 = engine.StackPop() as MOGRecord;

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
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have 2 items (type defaultValue).");

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
                            return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have a value with the good type in second position.");
                        }
                    }
                    else
                    {
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{key}: parameter", "default value list definition must have a type in first position.");
                    }
                }
                else
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{key}: parameter", "parameter definition is a type or a list (type defaultValue).");
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
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{p.VarName}: type is invalid !", $"{p.Type} expected", $"{pv.Type} provided");
                    }
                }
                else
                {
                    // On n'a pas de valeur fournie pour ce paramètre
                    // Si on a une valeur par défaut c'est pas grave, sinon erreur !

                    if (p.Value == null)
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"{p.VarName}: parameter is mandatory !");
                }
            }

            // On crée les variables
            // Normalement on ne devrait pas avoir de valeur à null
            // Pour le moment on ne bloque pas, on place juste MOGNull comme valeur dans ce cas là

            EvalResult result = EvalResult.NoError;

            foreach (ParamDefinition pdef in pDefinitions)
            {
                result = engine.VarWrite(pdef.VarName, pdef.Value ?? new MOGNull(engine));

                if (result != EvalResult.NoError)
                    break;
            }

            return result;
        }

        private static EvalResult PrimitiveBinaryAnd(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var n1 = engine.StackPop() as MOGNumber;

            var result = (int)n0.Value & (int)n1.Value;

            engine.StackPush(new MOGNumber(engine, result));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveBinaryOr(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var n1 = engine.StackPop() as MOGNumber;

            var result = (int)n0.Value | (int)n1.Value;

            engine.StackPush(new MOGNumber(engine, result));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveBinaryXor(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var n1 = engine.StackPop() as MOGNumber;

            var result = (int)n0.Value ^ (int)n1.Value;

            engine.StackPush(new MOGNumber(engine, result));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveBinaryComplement(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var result = ~(int)n0.Value;

            engine.StackPush(new MOGNumber(engine, result));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveRightShift(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n0 = engine.StackPop() as MOGNumber;
                var n1 = engine.StackPop() as MOGNumber;

                int v = (int)n1.Value >> (int)n0.Value;
                engine.StackPush(new MOGNumber(engine, v));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveLeftShift(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var n0 = engine.StackPop() as MOGNumber;
                var n1 = engine.StackPop() as MOGNumber;

                int v = (int)n1.Value << (int)n0.Value;
                engine.StackPush(new MOGNumber(engine, v));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveToFormat(MogwaiNanoEngine engine, string name)
        {
            // 50 "D3" ->format -----> "050"

            var s = engine.StackSign(2);
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString) && s[1] == typeof(MOGNumber))
            {
                var format = engine.StackPop() as MOGString;   
                var number = engine.StackPop() as MOGNumber;

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

                    engine.StackPush(new MOGString(engine, result));
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSub(MogwaiNanoEngine engine, string name)
        {
            // "ABCDE" 1 1 sub ---> "B"
            // "ABCDE" 2 0 sub   ---> "CDE"

            // (1 2 3 4 5) 1 1 sub ---> (2)
            // (1 2 3 4 5) 2 0 sub   ---> (3 4 5)

            // D:FFBBEE 0 2 sub ---> D:FFBB

            var sign = engine.StackSign(3);

            if (sign.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (sign[0] != typeof(MOGNumber) || sign[1] != typeof(MOGNumber) || (sign[2] != typeof(MOGString) && sign[2] != typeof(MOGList) && sign[2] != typeof(MOGData) && sign[2] != typeof(MOGRef)))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var n0 = engine.StackPop() as MOGNumber;
            var n1 = engine.StackPop() as MOGNumber;
            var n2 = engine.StackPop();

            var start = (int)n1.Value;
            var count = (int)n0.Value;

            if (start < 0 || count < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            if (n2 is MOGString s)
            {
                if (start >= s.Value.Length)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = s.Value.Length;

                if (start + count >= s.Value.Length)
                    count = s.Value.Length - start;

                try
                {
                    engine.StackPush(new MOGString(engine, s.Value.Substring(start, count)));
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, ex.Message);
                }
            }
            else if (n2 is MOGList l)
            {
                if (start < 0 || start >= l.Items.Count)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = l.Items.Count;

                if (start + count >= l.Items.Count)
                    count = l.Items.Count - start;

                var l2 = new MOGList(engine);

                for (int i = 0; i < count; i++)
                    l2.Items.Add(l.Items[start + i]);

                engine.StackPush(l2);
                return EvalResult.NoError;
            }
            else if (n2 is MOGData d)
            {
                if (start < 0 || start >= d.Items.Length)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                if (count <= 0)
                    count = d.Items.Length;

                if (start + count >= d.Items.Length)
                    count = d.Items.Length - start;

                var items = new byte[count];

                for (int i = 0; i < count; i++)
                    items[i] = d.Items[start + i];

                var d2 = new MOGData(engine, items);

                engine.StackPush(d2);

                return EvalResult.NoError;
            }
            else if (n2 is MOGRef r)
            {
                var value = engine.VarRead(r.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, r.ToString());

                engine.StackPush(value);
                engine.StackPush(n1);
                engine.StackPush(n0);

                return PrimitiveSub(engine, name);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveToNumber(MogwaiNanoEngine engine, string name)
        {
            // string ->num

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGString))
            {
                var @string = engine.StackPop() as MOGString;

                if (float.TryParse(@string.Value, out var value))
                {
                    engine.StackPush(new MOGNumber(engine, value));
                    return EvalResult.NoError;
                }

                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, @string.ToString());
            }       

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveToString(MogwaiNanoEngine engine, string name)
        {
            // object ->str

            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            var obj = engine.StackPop();

            if (obj is MOGString)
            {
                engine.StackPush(obj);
            }
            else
            {
                engine.StackPush(new MOGString(engine, obj.ToString())); 
            }

            return EvalResult.NoError;
        }

        #region TASKS

        private static EvalResult PrimitiveIsTask(MogwaiNanoEngine engine, string name)
        {
            engine.StackPush(new MOGBoolean(engine, engine.IsTask));    
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveTaskDef(MogwaiNanoEngine engine, string name)
        {
            // name function TASK.DEF

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGFunction) && s[1] == typeof(MOGName))
            {
                var function = engine.StackPop() as MOGFunction;
                var taskName = engine.StackPop() as MOGName;

                return engine.CreateTask(taskName.Value, function.ToStringCode());
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskList(MogwaiNanoEngine engine, string name)
        {
            var list = new MOGList(engine);

            foreach (string task in engine.Tasks.Keys)
            {
                list.AddItem(new MOGName(engine, task));
            }

            engine.StackPush(list);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveTaskStartWithParameter(MogwaiNanoEngine engine, string name)
        {
            // name object TASK.START
            // objet is a parameter for the task's job

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[1] == typeof(MOGName))
            {
                var parameter = engine.StackPop();
                var taskName = engine.StackPop() as MOGName;

                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, taskName.ToString());
                
                string paramString = null;

                if (parameter is not MOGNull)
                    paramString = parameter.ToString();

                return task.Start(paramString);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskStartWithoutParameter(MogwaiNanoEngine engine, string name)
        {
            // name TASK.START

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, taskName.ToString());

                return task.Start();
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskIsRunning(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                engine.StackPush(new MOGBoolean(engine, task.Status == MOGTask.TaskStatus.Running));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskStop(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                task.Stop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskPurge(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
                return engine.TaskPurge(taskName.Value);   
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskPublish(MogwaiNanoEngine engine, string name)
        {
            // message task.publish

            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var message = engine.StackPop();
            return engine.TaskPublish(message.ToString());
        }

        private static EvalResult PrimitiveTaskSend(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[1] == typeof(MOGName))
            {
                var message = engine.StackPop();
                var taskName = engine.StackPop() as MOGName;

                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, taskName.ToString());

                return task.SendMessage(message.ToString());
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskSetResult(MogwaiNanoEngine engine, string name)
        {
            // object task.setResult

            if (engine.StackSize == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            var obj = engine.StackPop();

            if (engine.IsTask)
            {
                ArrayList items = null;

                try
                {
                    items = engine.MotherEngine.Parse(obj.ToString());
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.ParseError, ex.Message);
                }

                engine.TaskResult = items[0] as MOGObject;
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveTaskGetResult(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
               
                if (engine.Tasks.Contains(taskName.Value))
                {
                    var task = engine.Tasks[taskName.Value] as MOGTask;
                    engine.StackPush(task.Result);
                    return EvalResult.NoError;
                }

                return EvalResult.Failure(engine, Error.UnknownNameError, name, taskName.Value);
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskGetName(MogwaiNanoEngine engine, string name)
        {
            if (engine.IsTask)
            {
                engine.StackPush(new MOGName(engine, engine.Name));
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.InvalidOutsideOfATaskError, name);
        }

        private static EvalResult PrimitiveTaskWait(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var taskName = engine.StackPop() as MOGName;
                var task = engine.GetTask(taskName.Value);

                if (task == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                return task.Wait();
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveTaskJoin(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGList))
            {
                var list = engine.StackPop() as MOGList;

                if (list.Items.Count > 0)
                {
                    ArrayList names = new();

                    foreach (var item in list.Items)
                    {
                        if (item is MOGName taskName)
                        {
                            names.Add(taskName.Value);
                        }
                        else
                        {
                            return EvalResult.Failure(engine, Error.BadArgumentValueError, name, item.ToString());
                        }
                    }

                    while (names.Count > 0)
                    {
                        Thread.Sleep(10);

                        for (int i = names.Count - 1; i >= 0; i--)
                        {
                            var taskName = names[i] as string;

                            if (!engine.Tasks.Contains(taskName))
                                return EvalResult.Failure(engine, Error.UnknownNameError, name, taskName);

                            var task = engine.Tasks[taskName] as MOGTask;

                            if (task.Status == MOGTask.TaskStatus.Waiting)
                                names.RemoveAt(i);
                        }

                        var r = engine.ExecuteWaitingFireObjects();

                        if (r != EvalResult.NoError)
                            return r;
                    }

                    return EvalResult.NoError;
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region UNITS

        private static EvalResult PrimitiveGetUnits(MogwaiNanoEngine engine, string name)
        {
            var list = new MOGList(engine);

            foreach (var unit in engine.Units)
                list.AddItem(new MOGName(engine, unit));
           
            engine.StackPush(list);
            
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveRunUnit(MogwaiNanoEngine engine, string name)
        {
            // 'unitName' runUnit

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var unitName = engine.StackPop() as MOGName;
            var unitFilename = Path.Combine(@"I:\mogwai\units", unitName.Value);

            if (File.Exists(unitFilename))
            {
                string unitCode;

                try
                {
                    unitCode = File.ReadAllText(unitFilename);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.UnableToReadUnitError, name, unitName.Value, ex.Message);
                }

                MOGFunction function;

                try
                {
                    function = new MOGFunction(engine, unitCode);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.ParseError, name, unitName.Value, ex.Message);
                }
                    
                var r = function.Execute();
                function = null;

                if (r.IsError)
                    return r;

                return EvalResult.NoError;
            }
            else
            {
                return EvalResult.Failure(engine, Error.UnknownUnitError, name, unitName.Value);
            }
        } 

        #endregion

        #region SKILLS

        private static EvalResult PrimitiveGetSkills(MogwaiNanoEngine engine, string name)
        {
            var list = new MOGList(engine);

            foreach (var skill in Skills)
                list.AddItem(new MOGName(engine, skill));

            engine.StackPush(list);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveHasSkill(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var skillName = engine.StackPop() as MOGName;
            var skillValue = skillName.Value.ToUpper();

            foreach (var skill in Skills)
            {
                if (skill == skillValue)        
                {
                    engine.StackPush(new MOGBoolean(engine, true));
                    return EvalResult.NoError;
                }
            }

            engine.StackPush(new MOGBoolean(engine, false));

            return EvalResult.NoError;
        }

        #endregion

        #region FLAGS

        private static EvalResult PrimitiveFlagSet(MogwaiNanoEngine engine, string name)
        {
            // 'name' flag.set

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var flagName = engine.StackPop() as MOGName;

            if (!engine.Flags.Contains(flagName.Value))
                engine.Flags.Add(flagName.Value);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveFlagClear(MogwaiNanoEngine engine, string name)
        {
            // 'name' flag.clear

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var flagName = engine.StackPop() as MOGName;

            if (engine.Flags.Contains(flagName.Value))
                engine.Flags.Remove(flagName.Value);

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveFlagIsSet(MogwaiNanoEngine engine, string name)
        {
            // 'name' flag.isSet

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var flagName = engine.StackPop() as MOGName;
            var v = engine.Flags.Contains(flagName.Value);
            engine.StackPush(new MOGBoolean(engine, v));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveFlagIsClear(MogwaiNanoEngine engine, string name)
        {
            // 'name' flag.isClear

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var flagName = engine.StackPop() as MOGName;
            var v = engine.Flags.Contains(flagName.Value);
            engine.StackPush(new MOGBoolean(engine, !v));

            return EvalResult.NoError;
        }

        #endregion

        #region STOPWATCHS

        private static EvalResult PrimitiveStopwatchCreate(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.create  

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.NameAlreadyExistsError, name, stopwatchName.Value);

                engine.Stopwatches.Add(stopwatchName.Value, new Stopwatch());

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchStart(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.start

            var s = engine.StackSign(1);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;
                
                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                sw.Start();
                
                return EvalResult.NoError;
            }
            
            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchStop(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.stop

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                sw.Stop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchReset(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.reset

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                sw.Reset();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchRestart(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.restart

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                sw.Restart();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchIsRunning(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.isRunning

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                engine.StackPush(new MOGBoolean(engine, sw.IsRunning));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchPurge(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.purge

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;
                    
                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                sw.Stop();

                engine.Stopwatches.Remove(stopwatchName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveStopwatchElapsed(MogwaiNanoEngine engine, string name)
        {
            // 'name' stopwatch.elapsed

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var stopwatchName = engine.StackPop() as MOGName;

                if (!engine.Stopwatches.Contains(stopwatchName.Value))
                    return EvalResult.Failure(engine, Error.UnknownNameError, name, stopwatchName.Value);

                var sw = engine.Stopwatches[stopwatchName.Value] as Stopwatch;
                engine.StackPush(new MOGNumber(engine, sw.ElapsedMilliseconds));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region GPIO

        private static EvalResult PrimitiveGpioModeInput(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.Input);

        private static EvalResult PrimitiveGpioSetModeInputPullDown(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.InputPullDown);

        private static EvalResult PrimitiveGpioSetModeInputPullUp(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.InputPullUp);

        private static EvalResult PrimitiveGpioSetModeOutput(MogwaiNanoEngine engine, string name) => SetPinMode(engine, name, PinMode.Output);
        
        private static EvalResult PrimitiveGpioPinWriteHigh(MogwaiNanoEngine engine, string name) => GpioPinWrite(engine, name, PinValue.High);

        private static EvalResult PrimitiveGpioPinWriteLow(MogwaiNanoEngine engine, string name) => GpioPinWrite(engine, name, PinValue.Low);

        private static EvalResult PrimitiveGpioPinRead(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = engine.GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.GpioUnknownPinError, name);

            var e = pin.Read();
            engine.StackPush(new MOGNumber(engine, e == PinValue.High ? 1 : 0));

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveGpioPinToggle(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = engine.GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.GpioUnknownPinError, name);

            pin.Toggle();

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveGpioPinClose(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (!engine.ClosePin(nPin))
                return EvalResult.Failure(engine, Error.GpioUnknownPinError, name);

            return EvalResult.NoError;
        }

        #endregion

        #region I2C

        private static EvalResult PrimitiveI2cOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' bus address i2c.open

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber) || s[2] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var address = engine.StackPop() as MOGNumber;
            var bus = engine.StackPop() as MOGNumber;
            var deviceName = engine.StackPop() as MOGName;

            int busNumber = (int)bus.Value;

            if (busNumber < 1 || busNumber > 2)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C bus number must be between 1 and 2");

            int addressNumber = (int)address.Value;

            if (addressNumber < 0 || addressNumber > 127)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C address number must be between 0 and 127");

            if (engine.I2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(engine, Error.I2cDeviceAlreadyOpenedError, name, $"I2C device (bus {busNumber}, address {addressNumber:X2}) is already opened");

            try
            {
                var i2cSettings = new I2cConnectionSettings(busNumber, addressNumber, I2cBusSpeed.FastMode);
                var i2cDevice = I2cDevice.Create(i2cSettings);

                engine.I2cDevices.Add(deviceName.Value, i2cDevice);
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.I2cDeviceOpenError, name, $"failed to open I2C device (bus {busNumber}, address {addressNumber:X2})", ex.Message);
            }

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveI2cClose(MogwaiNanoEngine engine, string name)
        {
            // name i2c.close   

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var deviceName = engine.StackPop() as MOGName;

            if (!engine.I2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(engine, Error.I2cUnknownDeviceNameError, name);

            var i2cDevice = engine.I2cDevices[deviceName.Value] as I2cDevice;
            engine.I2cDevices.Remove(deviceName.Value);

            i2cDevice.Dispose();

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveI2cWrite(MogwaiNanoEngine engine, string name)
        {
            // name data i2c.write

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGName))
            {   
                var data = engine.StackPop() as MOGData;
                var deviceName = engine.StackPop() as MOGName;

                if (!engine.I2cDevices.Contains(deviceName.Value))
                    return EvalResult.Failure(engine, Error.I2cUnknownDeviceNameError, name);

                var i2cDevice = engine.I2cDevices[deviceName.Value] as I2cDevice;

                try
                {
                    var r = i2cDevice.Write(data.ToSpanByte());

                    if (r.Status == I2cTransferStatus.FullTransfer)
                    {
                        return EvalResult.NoError;
                    }
                    else
                    {
                        return EvalResult.Failure(engine, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                    }
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGName))
            {
                var @ref = engine.StackPop() as MOGRef;
                var deviceName = engine.StackPop() as MOGName;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(deviceName);
                engine.StackPush(value);

                var r = PrimitiveI2cWrite(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveI2cRegisterWrite(MogwaiNanoEngine engine, string name)
        {
            // name register data i2c.write

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGName))
            {
                var data = engine.StackPop() as MOGData;
                var register = engine.StackPop() as MOGNumber;
                var deviceName = engine.StackPop() as MOGName;

                if (!engine.I2cDevices.Contains(deviceName.Value))
                    return EvalResult.Failure(engine, Error.I2cUnknownDeviceNameError, name);

                if (register.Value < 0 || register.Value > 255)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C register number must be between 0 and 255");

                var registerAddress = (byte)register.Value;

                var i2cDevice = engine.I2cDevices[deviceName.Value] as I2cDevice;

                try
                {
                    byte[] buffer = new byte[1 + data.Items.Length];
                    buffer[0] = registerAddress;
                    Array.Copy(data.Items, 0, buffer, 1, data.Items.Length);
                    var span = new SpanByte(buffer);
                    var r = i2cDevice.Write(span);

                    if (r.Status != I2cTransferStatus.FullTransfer)
                        return EvalResult.Failure(engine, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");

                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.I2cWriteError, name, $"failed to write to I2C device '{deviceName.Value}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGName))
            {
                var @ref = engine.StackPop() as MOGRef;
                var register = engine.StackPop() as MOGNumber;
                var deviceName = engine.StackPop() as MOGName;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name);

                engine.StackPush(deviceName);
                engine.StackPush(register);
                engine.StackPush(value);   

                var r = PrimitiveI2cRegisterWrite(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveI2cRead(MogwaiNanoEngine engine, string name)
        {
            // name length i2c.read

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var length = engine.StackPop() as MOGNumber;
            var deviceName = engine.StackPop() as MOGName;

            if (!engine.I2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(engine, Error.I2cUnknownDeviceNameError, name);
            
            if (length.Value < 0 || length.Value > 255)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C read length must be between 0 and 255");

            var i2cDevice = engine.I2cDevices[deviceName.Value] as I2cDevice;

            try
            {
                byte[] buffer = new byte[(int)length.Value];
                var span = new SpanByte(buffer);
                var r = i2cDevice.Read(span);

                if (r.Status == I2cTransferStatus.FullTransfer)
                {
                    var mogData = new MOGData(engine, buffer);
                    engine.StackPush(mogData);

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                }
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", ex.Message);
            }
        }

        private static EvalResult PrimitiveI2cRegisterRead(MogwaiNanoEngine engine, string name)
        {
            // name register length i2c.read

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber) || s[2] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var length = engine.StackPop() as MOGNumber;
            var register = engine.StackPop() as MOGNumber;
            var deviceName = engine.StackPop() as MOGName;

            if (!engine.I2cDevices.Contains(deviceName.Value))
                return EvalResult.Failure(engine, Error.I2cUnknownDeviceNameError, name);

            if (register.Value < 0 || register.Value > 255)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C register number must be between 0 and 255");

            var registerAddress = (byte)register.Value;

            if (length.Value < 0 || length.Value > 255)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C read length must be between 0 and 255");

            var count = (int)length.Value;

            var i2cDevice = engine.I2cDevices[deviceName.Value] as I2cDevice;
            
            try
            {
                byte[] writeBuffer = new byte[] { registerAddress };
                byte[] readBuffer = new byte[count];

                SpanByte writeSpan = new SpanByte(writeBuffer);
                SpanByte readSpan = new SpanByte(readBuffer);

                I2cTransferResult r = i2cDevice.WriteRead(writeSpan, readSpan);

                if (r.Status == I2cTransferStatus.FullTransfer)
                {
                    var mogData = new MOGData(engine, readBuffer);
                    engine.StackPush(mogData);

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", $"I2C transfer status: {r.Status}");
                }
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.I2cReadError, name, $"failed to read from I2C device '{deviceName.Value}'", ex.Message);
            }
        }

        private static EvalResult PrimitiveI2cScan(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var bus = engine.StackPop() as MOGNumber;
            int busNumber = (int)bus.Value;

            var list = new MOGList(engine);

            byte[] probe = new byte[1];
            SpanByte span = new SpanByte(probe);

            for (int address = 0x08; address <= 0x77; address++)
            {
                using (I2cDevice i2c = new(new I2cConnectionSettings(busNumber, address)))
                {
                    var res = i2c.Write(span);

                    if (res.Status == I2cTransferStatus.FullTransfer)
                        list.AddItem(new MOGNumber(engine, address));
                }
            }

            engine.StackPush(list);

            return EvalResult.NoError;
        }

        #endregion

        #region PWM

        private static EvalResult PrimitivePwmOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' pin frequency dutyCycle pwm.open

            var s = engine.StackSign(4);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGName))
            {
                var dutyCycle = engine.StackPop() as MOGNumber;
                var frequency = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;
                var pwmName = engine.StackPop() as MOGName;    

                if (dutyCycle.Value < 0 || dutyCycle.Value > 100)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "PWM duty cycle must be between 0 and 100");
                
                if (frequency.Value <= 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "PWM frequency must be greater than 0");
                
                if (engine.PwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(engine, Error.PwmAlreadyOpenedError, name);
                
                int nPin = (int)pin.Value;

                try
                {
                    var pwmChannel = PwmChannel.CreateFromPin(nPin, (int)frequency.Value, dutyCycle.Value / 100.0);
                    
                    if (pwmChannel == null)
                        return EvalResult.Failure(engine, Error.PwmOpenError, name, $"failed to open PWM on pin {nPin}");

                    engine.PwmChannels.Add(pwmName.Value, pwmChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.PwmOpenError, name, $"failed to open PWM on pin {nPin}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' pwm.close

            var s = engine.StackSign(1);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;

                if (!engine.PwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(engine, Error.PwmUnknownNameError, name);
                
                var pwmChannel = engine.PwmChannels[pwmName.Value] as PwmChannel;               
                pwmChannel.Stop();
                pwmChannel.Dispose();
               
                engine.PwmChannels.Remove(pwmName.Value);
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmStart(MogwaiNanoEngine engine, string name)
        {
            // name pwm.start

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;

                if (!engine.PwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(engine, Error.PwmUnknownNameError, name);

                var pwmChannel = engine.PwmChannels[pwmName.Value] as PwmChannel;
                pwmChannel.Start();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitivePwmStop(MogwaiNanoEngine engine, string name)
        {
            // name pwm.stop

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var pwmName = engine.StackPop() as MOGName;

                if (!engine.PwmChannels.Contains(pwmName.Value))
                    return EvalResult.Failure(engine, Error.PwmUnknownNameError, name);

                var pwmChannel = engine.PwmChannels[pwmName.Value] as PwmChannel;
                pwmChannel.Stop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region ADC

        private static EvalResult PrimitiveAdcOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' channel adc.open

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGName))
            {
                var channel = engine.StackPop() as MOGNumber;
                var adcName = engine.StackPop() as MOGName;
             
                if (engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcAlreadyOpenedError, name);

                int nChannel = (int)channel.Value;

                try
                {
                    var adcChannel = engine.AdcController.OpenChannel(nChannel);
                    engine.AdcChannels.Add(adcName.Value, adcChannel);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.AdcOpenError, name, $"failed to open ADC channel {nChannel}", ex.Message);
                }

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcClose(MogwaiNanoEngine engine, string name)
        {
            // 'name' adc.close

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;

                if (!engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcUnknownNameError, name);

                var adcChannel = engine.AdcChannels[adcName.Value] as AdcChannel;
                adcChannel.Dispose();

                engine.AdcChannels.Remove(adcName.Value);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcReadValue(MogwaiNanoEngine engine, string name)
        {
            // name adc.readValue

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var adcName = engine.StackPop() as MOGName;

                if (!engine.AdcChannels.Contains(adcName.Value))
                    return EvalResult.Failure(engine, Error.AdcUnknownNameError, name);

                var adcChannel = engine.AdcChannels[adcName.Value] as AdcChannel;
                var value = adcChannel.ReadValue();

                engine.StackPush(new MOGNumber(engine, value));

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveAdcGetMaxValue(MogwaiNanoEngine engine, string name)
        {
            // adc.maxValue

            var maxValue = engine.AdcController.MaxValue;
            engine.StackPush(new MOGNumber(engine, maxValue));
            
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveAdcGetResolutionInBits(MogwaiNanoEngine engine, string name)
        {
            // adc.resolutionInBits

            var resolutionInBits = engine.AdcController.ResolutionInBits;
            engine.StackPush(new MOGNumber(engine, resolutionInBits));

            return EvalResult.NoError;
        }

        #endregion

        #region SSD1306 OLED SCREEN

        private static EvalResult PrimitiveSsd1306Init(MogwaiNanoEngine engine, string name)
        {
            // bus address ssd1306.init

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var address = engine.StackPop() as MOGNumber;
                var bus = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 != null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsOpenedError, name);

                int busNumber = (int)bus.Value;
                int addressNumber = (int)address.Value;

                if (busNumber < 1 || busNumber > 2)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C bus number must be between 1 and 2");
                
                if (addressNumber < 0 || addressNumber > 127)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "I2C address number must be between 0 and 127");

                if (engine.Ssd1306 != null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsOpenedError, name);

                try
                {
                    I2cConnectionSettings settings = new(busNumber, addressNumber, I2cBusSpeed.FastMode);
                    I2cDevice i2cDevice = I2cDevice.Create(settings);

                    engine.Ssd1306 = new(i2cDevice, DisplayResolution.OLED128x64);
                    engine.Ssd1306.ClearScreen();
                }
                catch (Exception ex)
                {
                    if (engine.Ssd1306 != null)
                    {
                        engine.Ssd1306.Dispose();
                        engine.Ssd1306 = null;
                    }

                    return EvalResult.Failure(engine, Error.Ssd1306InitError, name, "failed to initialize ssd1306 display", $"bus {busNumber}", $"address {addressNumber:X2}", ex.Message);
                }
                
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306Close(MogwaiNanoEngine engine, string name)
        {
            if (engine.Ssd1306 != null)
            { 
                engine.Ssd1306.Dispose();
                engine.Ssd1306 = null;
            }   

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveSsd1306Clear(MogwaiNanoEngine engine, string name)
        {
            if (engine.Ssd1306 == null)
                return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);
            
            engine.Ssd1306.ClearScreen();
            
            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveSsd1306PrintString(MogwaiNanoEngine engine, string name)
        {
            // x y text size center ssd1306.printString

            var s = engine.StackSign(5);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = engine.StackPop() as MOGBoolean;
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    if (engine.Ssd1306.Font == null)
                        engine.Ssd1306.Font = new BasicFont();

                    engine.Ssd1306.Write((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;  
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);  
        }

        private static EvalResult PrimitiveSsd1306DrawString(MogwaiNanoEngine engine, string name)
        {
            // x y text size center ssd1306.drawString

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGString) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var center = engine.StackPop() as MOGBoolean;
                var size = engine.StackPop() as MOGNumber;
                var text = engine.StackPop() as MOGString;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    if (engine.Ssd1306.Font == null)
                        engine.Ssd1306.Font = new BasicFont();

                    engine.Ssd1306.DrawString((int)x.Value, (int)y.Value, text.Value, (byte)size.Value, center.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306Refresh(MogwaiNanoEngine engine, string name)
        {
            if (engine.Ssd1306 == null)
                return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

            engine.Ssd1306.Display();

            return EvalResult.NoError;
        }

        private static EvalResult PrimitiveSsd1306DrawPixel(MogwaiNanoEngine engine, string name)
        {
            // x y true ssd1306.drawPixel   

            var s = engine.StackSign(3);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;
                
                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    engine.Ssd1306.DrawPixel((int)x.Value, (int)y.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306DrawHorizontalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len true ssd1306.drawHorizontalLine  

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var len = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    engine.Ssd1306.DrawHorizontalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306DrawHVerticalLine(MogwaiNanoEngine engine, string name)
        {
            // x y len true ssd1306.drawVerticalLine 

            var s = engine.StackSign(4);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var len = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    engine.Ssd1306.DrawVerticalLine((int)x.Value, (int)y.Value, (int)len.Value, set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306DrawRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h true ssd1306.drawRectangle

            var s = engine.StackSign(5);
            
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    int vx = (int)x.Value;
                    int vy = (int)y.Value;
                    int vw = (int)width.Value;
                    int vh = (int)height.Value;

                    engine.Ssd1306.DrawHorizontalLine(vx, vy, vw,  set.Value);
                    engine.Ssd1306.DrawVerticalLine(vx + vw - 1,vy, vh, set.Value);
                    engine.Ssd1306.DrawHorizontalLine(vx, vy + vh - 1, vw, set.Value);
                    engine.Ssd1306.DrawVerticalLine(vx, vy, vh, set.Value);
                    
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306DrawFilledRectangle(MogwaiNanoEngine engine, string name)
        {
            // x y w h true ssd1306.drawFilledRectangle

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGBoolean) && s[1] == typeof(MOGNumber) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber))
            {
                var set = engine.StackPop() as MOGBoolean;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    engine.Ssd1306.DrawFilledRectangle((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value,set.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        private static EvalResult PrimitiveSsd1306DrawBitmap(MogwaiNanoEngine engine, string name)
        {
            // x y w h data size ssd1306.drawBitmap

            var s = engine.StackSign(6);
                
            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGData) && s[2] == typeof(MOGNumber) && s[3] == typeof(MOGNumber) && s[4] == typeof(MOGNumber) && s[5] == typeof(MOGNumber))
            {
                var size = engine.StackPop() as MOGNumber;
                var data = engine.StackPop() as MOGData;
                var height = engine.StackPop() as MOGNumber;
                var width = engine.StackPop() as MOGNumber;
                var y = engine.StackPop() as MOGNumber;
                var x = engine.StackPop() as MOGNumber;

                if (engine.Ssd1306 == null)
                    return EvalResult.Failure(engine, Error.Ssd1306IsClosedError, name);

                try
                {
                    engine.Ssd1306.DrawBitmap((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value, data.Items, (byte)size.Value);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.Ssd1306OperationError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        #endregion

        #region DEVICE

        private static EvalResult PrimitiveDeviceSetPinFunction(MogwaiNanoEngine engine, string name)
        {
            // pin setvalue device.setPin

            var s = engine.StackSign(2);    

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);  

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var setValue = engine.StackPop() as MOGNumber;
                var pin = engine.StackPop() as MOGNumber;  

                if (pin.Value < 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

                if (SystemInfo.Platform == "ESP32")
                {
                    try
                    {
                        nanoFramework.Hardware.Esp32.Configuration.SetPinFunction((int)pin.Value, (nanoFramework.Hardware.Esp32.DeviceFunction)(int)setValue.Value);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, Error.PlatformNotSupportedError, name, ex.Message);
                    }

                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.PlatformNotSupportedError, name);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        #endregion

        #endregion

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

        #region VARS

        public EvalResult VarWrite(string name, MOGObject value)
        {
            // This name is used by a func ?

            if (Functions.Contains(name))
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
            if (Functions.Contains(name))
                return Functions[name] as MOGFunction;

            return null;
        }

        #endregion

        #region FIREOBJECTS

        public void RegisterFireObject(MOGFireObject fireObject)
        {
            lock (_fireObjectsQueueLock)
                _fireObjectsQueue.Enqueue(fireObject);
        }

        public void CleanupWaitingFireObjects()
        {
            lock (_fireObjectsQueueLock)
                _fireObjectsQueue.Clear();
        }

        public bool HasWaitingFireObjects => !DisableInterrupts && _fireObjectsQueue.Count > 0;

        public EvalResult ExecuteWaitingFireObjects()
        {
            var result = EvalResult.NoError;

            if (!DisableInterrupts && _fireObjectsQueue.Count > 0)
            {
                MOGFireObject fireObject = null;

                lock (_fireObjectsQueueLock)
                    fireObject = _fireObjectsQueue.Dequeue() as MOGFireObject;

                AddNewStack();

                result = fireObject.Function.Execute();

                RemoveLastStack();

                fireObject = null;
            }

            return result;
        }

        #endregion

        #region TIMERS

        public EvalResult PurgeTimer(string name)
        {
            if (Timers.Contains(name))
            {
                var timer = Timers[name] as MOGTimer;
                timer.Stop();
                Timers.Remove(name);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.UnknownNameError, $"unabled to purge unknown '{name}' timer.");
        }

        public EvalResult CreateNewTimer(string name, int interval, bool isCyclic, MOGFunction function, bool isLaterTimer = false)
        {
            if (Timers.Contains(name))
                return EvalResult.Failure(this, Error.NameAlreadyExistsError, $"timer '{name}' already exists.");

            if (interval < 0)
                return EvalResult.Failure(this, Error.BadArgumentValueError, "timer interval must be a positive value.");

            var timer = new MOGTimer(this, name, interval, isCyclic, function, isLaterTimer);
            Timers.Add(name, timer);

            return EvalResult.NoError;
        }

        public void CleanupTimers()
        {
            foreach (var key in Timers.Keys)
            {
                var timer = Timers[key] as MOGTimer;
                timer.Stop();
            }

            Timers.Clear();
        }

        #endregion

        #region EVENTS

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

        public void CleanupEvents()
        {
            _events.Clear();
        }

        #endregion

        #region STOPWATCHS

        private void CleanupStopwatchs()
        {
            foreach (var key in Stopwatches.Keys)
            {
                var stopwatch = Stopwatches[key] as Stopwatch;
                stopwatch.Stop();
            }

            Stopwatches.Clear();
        }

        #endregion

        #region GPIO

        public GpioPin GetPin(int pinNumber)
        {
            if (OpenedPins.Contains(pinNumber))
                return OpenedPins[pinNumber] as GpioPin;

            return null;
        }

        public bool ClosePin(int pinNumber)
        {
            if (OpenedPins.Contains(pinNumber))
            {
                var pin = OpenedPins[pinNumber] as GpioPin;
                OpenedPins.Remove(pin);
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
                return true;
            }

            return false;
        }

        private static EvalResult GpioPinWrite(MogwaiNanoEngine engine, string name, PinValue pinValue)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var pin = engine.GetPin((int)numPin.Value);

            if (pin == null)
                return EvalResult.Failure(engine, Error.GpioUnknownPinError, name);

            pin.Write(pinValue);

            return EvalResult.NoError;
        }

        private void CleanupOpenPins()
        {
            foreach (int pinNumber in OpenedPins.Keys)
            {
                var pin = OpenedPins[pinNumber] as GpioPin;
                pin.ValueChanged -= GpioPin_ValueChanged;
                pin.Dispose();
            }

            OpenedPins.Clear();
        }

        private static EvalResult SetPinMode(MogwaiNanoEngine engine, string name, PinMode mode)
        {
            // Used by all pin mode known
            // 4 gpio.setMode.xxx

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var numPin = engine.StackPop() as MOGNumber;

            if (numPin.Value < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name);

            var nPin = (int)numPin.Value;

            if (engine.OpenedPins.Contains(nPin))
            {
                var pin = engine.   OpenedPins[nPin] as GpioPin;
                pin.SetPinMode(mode);
            }
            else
            {
                GpioPin newPin = engine.GpioController.OpenPin(nPin, mode);
                engine.OpenedPins.Add(nPin, newPin);

                newPin.ValueChanged += engine.GpioPin_ValueChanged;
            }

            return EvalResult.NoError;
        }

        public void GpioPin_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            var eventType = e.ChangeType == PinEventTypes.Rising ? 1 : 0;

            var record = new MOGRecord(this);
            record.SetItem("pin", new MOGNumber(this, e.PinNumber));
            record.SetItem("eventType", new MOGNumber(this, eventType));

            FireEvent("GPIO_PIN_CHANGED", record);
        }

        #endregion

        #region I2C

        public void CleanupI2cDevices()
        {
            foreach (var key in I2cDevices.Keys)
            {
                var i2cDevice = I2cDevices[key] as I2cDevice;
                i2cDevice.Dispose();
            }

            I2cDevices.Clear();
        }

        public void CleanupAdcChannels()
        {
            foreach (var key in AdcChannels.Keys)
            {
                var adcChannel = AdcChannels[key] as AdcChannel;
                adcChannel.Dispose();
            }

            AdcChannels.Clear();
        }

        public void CleanupPwmChannels()
        {
            foreach (var key in PwmChannels.Keys)
            {
                var pwmChannel = PwmChannels[key] as PwmChannel;

                pwmChannel.Stop();
                pwmChannel.Dispose();
            }

            PwmChannels.Clear();
        }

        #endregion

        #region TASKS

        internal EvalResult CreateTask(string name, string code)
        {
            if (Tasks.Contains(name))
                return EvalResult.Failure(this, Error.NameAlreadyExistsError);

            try
            {
                Tasks[name] = new MOGTask(this, name, code);
                return EvalResult.NoError;
            }
            catch
            {

            }

            return EvalResult.Failure(this, Error.TaskCreationError);
        }

        internal MOGTask GetTask(string name)
        {
            if (Tasks.Contains(name))
                return Tasks[name] as MOGTask;
            
            return null;
        }

        internal EvalResult TaskPurge(string name)
        {
            if (Tasks.Contains(name))
            {
                Tasks.Remove(name);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(this, Error.UnknownNameError, $"unabled to purge unknown task '{name}'");
        }

        internal EvalResult TaskPublish(string message)
        {
            if (IsTask)
            {
                ArrayList items = null;

                try
                {
                    items = MotherEngine.Parse(message);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(this, Error.ParseError, ex.Message);
                }

                var messageInformations = new MOGRecord(MotherEngine!);
                messageInformations.SetItem("task", new MOGName(MotherEngine, Name));
                messageInformations.SetItem("message",items[0] as MOGObject);

                return MotherEngine.FireEvent(MOGTask.EVENT_TASK_DID_PUBLISH, messageInformations);
            }

            return EvalResult.NoError;
        }

        internal void CleanupTasks()
        {
            foreach (var key in Tasks.Keys)
            {
                var task = Tasks[key] as MOGTask;
                task.Stop();
            }

            while (true)
            {
                int countRunning = 0;

                foreach (var key in Tasks.Keys)
                {
                    var task = Tasks[key] as MOGTask;
                    
                    if (task.Status == MOGTask.TaskStatus.Running)
                        countRunning++;
                }

                if (countRunning == 0)
                    break;
            }

            Tasks.Clear();
        }

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
