// Copyright 2015-2026 Stéphane Sibué
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
using MogwaiNano.Objects;
using System;

namespace MogwaiNano.Interfaces
{
    public interface IDelegate
    {
        #region PROGRAM LIFECYCLE

        void ProgramStart(MogwaiNanoEngine engine, string code);

        void ProgramEnd(MogwaiNanoEngine engine, EvalResult result);

        EvalResult EngineDidPause(MogwaiNanoEngine engine) => EvalResult.NoError;

        EvalResult EngineDidResume(MogwaiNanoEngine engine) => EvalResult.NoError;

        #endregion

        #region DEBUG

        EvalResult DebugMessage(MogwaiNanoEngine engine, string message) => EvalResult.NoError;

        EvalResult DebugClear(MogwaiNanoEngine engine) => EvalResult.NoError;

        #endregion

        #region CONSOLE

        EvalResult ConsoleClearScreen(MogwaiNanoEngine engine) => EvalResult.NoError;

        EvalResult ConsolePrintLn(MogwaiNanoEngine engine, string message) => EvalResult.NoError;

        EvalResult ConsolePrint(MogwaiNanoEngine engine, string message) => EvalResult.NoError;

        #endregion
    }
}
