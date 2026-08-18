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
    public class MogwaiNanoErrors
    {
        public static Error ConnectionToDeviceError { get; } = new Error("MW.5000", $"connection to device error", Error.ErrorType.User);

        public static Error DeviceNotConnectedError { get; } = new Error("MW.5001", $"device not connected error", Error.ErrorType.User);

        public static Error DeviceUnreachableError { get; } = new Error("MW.5002", $"device unreachable error", Error.ErrorType.User);

        public static Error DeviceBusyError { get; } = new Error("MW.5003", $"device busy error", Error.ErrorType.User);

        public static Error DeviceIsNotRunningError { get; } = new Error("MW.5004", $"device is not running error", Error.ErrorType.User);

        public static Error BadDeviceResponse { get; } = new Error("MW.5005", $"bad device response error", Error.ErrorType.User);
    }
}
