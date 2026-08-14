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

using MOGWAI.Engine;

namespace MogwaiNanoStudio
{
    internal class AppGlobal
    {
        public const int TCP_PORT = 9597;

        public const int DISCOVERY_PORT = 1968;

        public const string SOURCE_NAME = "STUDIO_NANO";

        public static MogwaiNanoClient NanoClient { get; } = new MogwaiNanoClient();

        public static MogwaiNanoRuntime NanoRuntime { get; private set; }

        public static MogwaiEngine MogwaiEngine { get; private set; }

        public static EngineDelegate EngineDelegate { get; private set; }

        static AppGlobal()
        {
            MogwaiEngine = new MogwaiEngine("MOGWAI NANO", true, true);

            EngineDelegate = new EngineDelegate(MogwaiEngine);
            MogwaiEngine.Delegate = EngineDelegate;

            NanoRuntime = new MogwaiNanoRuntime(MogwaiEngine);
        }

        public static void SetParserInNanoMode(bool nanoMode)
        {
            var value = !nanoMode;

            MogwaiEngine.SugarBehavior.AllowAfterDo = value;
            MogwaiEngine.SugarBehavior.AllowClassDo = value;
            MogwaiEngine.SugarBehavior.AllowDeclare = value;
            MogwaiEngine.SugarBehavior.AllowDoWhile = value;
            MogwaiEngine.SugarBehavior.AllowDuringDo = value;
            MogwaiEngine.SugarBehavior.AllowForeachDo = value;
            MogwaiEngine.SugarBehavior.AllowForeachFilterDo = value;
            MogwaiEngine.SugarBehavior.AllowForeachTransformDo = value;
            MogwaiEngine.SugarBehavior.AllowForeachFilterDo = value;
            MogwaiEngine.SugarBehavior.AllowGuardElse = value;
            MogwaiEngine.SugarBehavior.AllowPipeRef = value;
            MogwaiEngine.SugarBehavior.AllowPost = value;
            MogwaiEngine.SugarBehavior.AllowStoDivide = value;
            MogwaiEngine.SugarBehavior.AllowStoMultiply = value;
            MogwaiEngine.SugarBehavior.AllowStoPlus = value;
            MogwaiEngine.SugarBehavior.AllowStoSubstract = value;
            MogwaiEngine.SugarBehavior.AllowSwitch = value;
            MogwaiEngine.SugarBehavior.AllowTask = value;
            MogwaiEngine.SugarBehavior.AllowToParamsDo = value;
            MogwaiEngine.SugarBehavior.AllowToParamsReturnsDo = value;
            MogwaiEngine.SugarBehavior.AllowToReturnsDo = value;
            MogwaiEngine.SugarBehavior.AllowToWithDo = value;
            MogwaiEngine.SugarBehavior.AllowToWithReturnsDo = value;
            MogwaiEngine.SugarBehavior.AllowTrap = value;
        }
    }
}
