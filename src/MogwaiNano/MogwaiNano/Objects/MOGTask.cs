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

using MogwaiNano.Engine;
using System;
using System.Collections;
using System.Threading;

namespace MogwaiNano.Objects
{
    internal class MOGTask
    {
        public const string EVENT_TASK_DID_START = "TASK_DID_START";
        public const string EVENT_TASK_DID_END = "TASK_DID_END";
        public const string EVENT_TASK_DID_FAIL = "TASK_DID_FAIL";
        public const string EVENT_TASK_DID_PUBLISH = "TASK_DID_PUBLISH";
        public const string EVENT_TASK_DID_RECEIVE = "TASK_DID_RECEIVE";

        public enum TaskStatus
        {
            Waiting,
            Running,
        }

        private Thread _thread;
        private bool _isRunning;

        public string Name { get; init; }

        public string Job { get; init; }

        public MogwaiNanoEngine MotherEngine { get; private set; }

        public MogwaiNanoEngine TaskEngine { get; private set; }

        public TaskStatus Status
        {
            get
            {
                if (_isRunning)
                    return TaskStatus.Running;

                return TaskStatus.Waiting;
            }
        }

        public MOGObject Result
        {
            get => TaskEngine.TaskResult;

            set => TaskEngine.TaskResult = value;
        }

        public EvalResult LastEvalResult { get; set; } = EvalResult.NoError;

        public MOGTask(MogwaiNanoEngine engine, string name, string code)
        {
            Name = name;
            Job = code;
            MotherEngine = engine;

            TaskEngine = new MogwaiNanoEngine($"TASK ENGINE OF {MotherEngine.Name} MOTHER ENGINE")
            {
                TaskName = name,
                MotherEngine = MotherEngine,
                Delegate = MotherEngine.Delegate,
            };
        }

        public EvalResult Start(string parameter = null)
        {
            if (_isRunning)
                return EvalResult.Failure(MotherEngine, Error.TaskCreationError, Name, "Task is already running.");

            if (!string.IsNullOrEmpty(parameter))
            {
                ArrayList items;

                try
                {
                    items = TaskEngine.Parse(parameter);
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(MotherEngine, Error.UnabledToStartTaskError, Name, ex.Message);
                }

                for (int i = items.Count - 1; i >= 0; i--)
                    TaskEngine.StackPush(items[i] as MOGObject);
            }

            _isRunning = true;

            _thread = new Thread(() =>
            {
                LastEvalResult = TaskEngine.Run(Job, false); 
                _isRunning = false;
            });

            _thread.Start();

            return EvalResult.NoError;
        }

        public void Stop()
        {
            if (_isRunning)
                TaskEngine.HaltRequested = true;
        }

        public void ReapIfFinished()
        {
            if (_thread != null && !_isRunning)
            {
                _thread.Join();
                _thread = null;
            }
        }

        public void Dispose()
        {
            Stop();
            ReapIfFinished();

            TaskEngine.Delegate = null;
            TaskEngine = null;
            MotherEngine = null;
            LastEvalResult = null;
        }

        public EvalResult SendMessage(string message)
        {
            ArrayList items = null;

            try
            {
                items = TaskEngine.Parse(message);
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(MotherEngine, Error.ParseError, ex.Message);
            }

            return TaskEngine.FireEvent(MOGTask.EVENT_TASK_DID_RECEIVE, items[0] as MOGObject);
        }

        public EvalResult Wait()
        {
            while (_thread != null)
            {
                Thread.Sleep(10);
                MotherEngine.ExecuteWaitingFireObjects();
            }

            ReapIfFinished();

            return LastEvalResult;
        }
    }
}
