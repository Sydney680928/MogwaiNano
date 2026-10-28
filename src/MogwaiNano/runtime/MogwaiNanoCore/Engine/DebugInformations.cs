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

using System;

namespace MogwaiNano.Engine
{
    public class DebugInformations
    {
        public int StartPos { get; set; }

        public int EndPos { get; set; }

        public DebugInformations()
        {

        }

        public DebugInformations(int startPos, int endPos)
        {
            StartPos = startPos;
            EndPos = endPos;
        }

        public DebugInformations(string debugInfo)
        {
            // 56;34
            // 345;34

            var parts = debugInfo.Split(';');

            if (parts.Length != 2)
                throw new ArgumentException("Invalid debug information format.");

            try
            {
                StartPos = int.Parse(parts[0]);
                EndPos = int.Parse(parts[1]);
            }
            catch (Exception ex)
            {
              
            }
        }
    }
}
