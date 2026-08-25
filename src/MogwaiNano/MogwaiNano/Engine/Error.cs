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

using System.Collections;

namespace MogwaiNano.Engine
{
    public class Error
    {
        private static Hashtable _errors;
        private static bool _initialized = false;

        private static Error 
            _none, 
            _parseError, 
            _haltEncounteredError, 
            _unableToFireEventError,                             
            _operationNotSupportedError, 
            _tooFewArgumentsError, 
            _badArgumentTypeError, 
            _badArgumentValueError,                             
            _divisionByZeroError, 
            _mathematicalError, 
            _convertError, 
            _unknownNameError,                   
            _nameAlreadyExistsError, 
            _functionAlreadyExistsError, 
            _nameAlreadyUsedByFunctionError,
            _nameAlreadyUsedByVarError, 
            _invalidNameError, 
            _unableToWriteValueError, 
            _unknownWordError,
            _gpioUnknownPinError,
            _i2cDeviceAlreadyOpenedError,
            _i2cDeviceOpenError,
            _i2cUnknownDeviceNameError,
            _i2cWriteError,
            _i2cReadError,
            _fatalError;

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _errors = new Hashtable();

            _none = RegisterError("MW.0", "OK");
            _parseError = RegisterError("MW.1", "parse error");
            _haltEncounteredError = RegisterError("MW.2", "halt encountered error");
            _unableToFireEventError = RegisterError("MW.6", "unable to fire event error");
            _operationNotSupportedError = RegisterError("MW.7", "operation not supported error");
            _tooFewArgumentsError = RegisterError("MW.20", "too few arguments error");
            _badArgumentTypeError = RegisterError("MW.21", "bad argument type error");
            _badArgumentValueError = RegisterError("MW.22", "bad argument value error");
            _divisionByZeroError = RegisterError("MW.30", "division by zero error");
            _mathematicalError = RegisterError("MW.31", "mathematical error");
            _convertError = RegisterError("MW.32", "convert error");
            _unknownNameError = RegisterError("MW.40", "unknown name error");
            _nameAlreadyExistsError = RegisterError("MW.41", "name already exists error");
            _functionAlreadyExistsError = RegisterError("MW.42", "function already exists error");
            _nameAlreadyUsedByFunctionError = RegisterError("MW.43", "name already used by function error");
            _nameAlreadyUsedByVarError = RegisterError("MW.44", "name already used by var error");
            _invalidNameError = RegisterError("MW.46", "invalid name error");
            _unableToWriteValueError = RegisterError("MW.47", "unable to write value in var error");
            _unknownWordError = RegisterError("MW.50", "unknown word error");
            _fatalError = RegisterError("MW.!!!", "fatal error");

            _gpioUnknownPinError = RegisterError("MW.500", "gpio unknown pin error");
            
            _i2cDeviceAlreadyOpenedError = RegisterError("MW.510", "i2c device already opened error");             
            _i2cDeviceOpenError = RegisterError("MW.511", "i2c device open error");
            _i2cUnknownDeviceNameError = RegisterError("MW.512", "i2c unknown device name error");
            _i2cWriteError = RegisterError("MW.513", "i2c write error");
            _i2cReadError = RegisterError("MW.514", "i2c read error");
        }

        private static Error RegisterError(string code, string message)
        {
            var error = new Error(code, message);
            _errors.Add(code, error);
            return error;
        }

        public static Hashtable Errors { get { EnsureInitialized(); return _errors; } }
        
        public static Error None { get { EnsureInitialized(); return _none; } }
        
        public static Error ParseError { get { EnsureInitialized(); return _parseError; } }
        
        public static Error HaltEncounteredError { get { EnsureInitialized(); return _haltEncounteredError; } }
        
        public static Error UnableToFireEventError { get { EnsureInitialized(); return _unableToFireEventError; } }
        
        public static Error OperationNotSupportedError { get { EnsureInitialized(); return _operationNotSupportedError; } }
        
        public static Error TooFewArgumentsError { get { EnsureInitialized(); return _tooFewArgumentsError; } }
        
        public static Error BadArgumentTypeError { get { EnsureInitialized(); return _badArgumentTypeError; } }
        
        public static Error BadArgumentValueError { get { EnsureInitialized(); return _badArgumentValueError; } }
        
        public static Error DivisionByZeroError { get { EnsureInitialized(); return _divisionByZeroError; } }
       
        public static Error MathematicalError { get { EnsureInitialized(); return _mathematicalError; } }
        
        public static Error ConvertError { get { EnsureInitialized(); return _convertError; } }
        
        public static Error UnknownNameError { get { EnsureInitialized(); return _unknownNameError; } }
        
        public static Error NameAlreadyExistsError { get { EnsureInitialized(); return _nameAlreadyExistsError; } }
        
        public static Error FunctionAlreadyExistsError { get { EnsureInitialized(); return _functionAlreadyExistsError; } }
        
        public static Error NameAlreadyUsedByFunctionError { get { EnsureInitialized(); return _nameAlreadyUsedByFunctionError; } }
        
        public static Error NameAlreadyUsedByVarError { get { EnsureInitialized(); return _nameAlreadyUsedByVarError; } }
        
        public static Error InvalidNameError { get { EnsureInitialized(); return _invalidNameError; } }
        
        public static Error UnableToWriteValueError { get { EnsureInitialized(); return _unableToWriteValueError; } }
        
        public static Error UnknownWordError { get { EnsureInitialized(); return _unknownWordError; } }
        
        public static Error GpioUnknownPinError { get { EnsureInitialized(); return _gpioUnknownPinError; } }

        public static Error I2cDeviceAlreadyOpenedError { get { EnsureInitialized(); return _i2cDeviceAlreadyOpenedError; } }

        public static Error I2cDeviceOpenError { get { EnsureInitialized(); return _i2cDeviceOpenError; } }

        public static Error I2cUnknownDeviceNameError { get { EnsureInitialized(); return _i2cUnknownDeviceNameError; } }

        public static Error I2cWriteError { get { EnsureInitialized(); return _i2cWriteError; } }

        public static Error I2cReadError { get { EnsureInitialized(); return _i2cReadError; } }

        public static Error FatalError { get { EnsureInitialized(); return _fatalError; } }

        public string Code { get; set; }
        
        public string Message { get; set; }
        
        public bool IsOK => Code == None.Code;

        public Error(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public override string ToString()
        {
            if (Message == null || Code == null)
            {

            }

            return $"{Message} ({Code})";
        }
    }
}
