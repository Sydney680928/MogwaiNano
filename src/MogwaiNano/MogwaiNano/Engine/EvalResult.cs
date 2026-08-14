using System;
using System.Text;

namespace MogwaiNano.Engine
{
    public class EvalResult
    {
        private static EvalResult _noError;

        public static EvalResult NoError
        {
            get
            {
                if (_noError == null)
                    _noError = new EvalResult();

                return _noError;
            }
        }

        public Error Error { get; init; } = Error.None;

        public string[] Informations { get; init; }

        public TimeSpan Duration { get; set; }

        public bool IsError => Error != Error.None;

        public bool IsSuccess => Error == Error.None;

        static EvalResult()
        {

        }

        public static EvalResult Failure(MogwaiNanoEngine engine, Error error, params string[] informations)
        {
            engine.LastError = error;

            return new EvalResult
            {
                Error = error,
                Informations = informations
            };
        }

        public static EvalResult ParseFailure(MogwaiNanoEngine engine, params string[] informations)
        {
            engine.LastError = Error.ParseError;

            return new EvalResult
            {
                Error = engine.LastError,
                //StartErrorPosition = engine.LastParserStartErrorPosition,
                //EndErrorPosition = engine.LastParserEndErrorPosition,
                //ExecutionContext = engine.LastParserExecutionContext,
                Informations = informations
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("MOGWAI NANO");

            if (Error == Error.None)
            {
                sb.AppendLine("OK");
            }
            else
            {
                sb.AppendLine(Error.ToString());

                if (Informations.Length > 0)
                {
                    foreach (var info in Informations)
                        sb.AppendLine(info);
                }
            }

            if (Duration > TimeSpan.Zero)
                sb.AppendLine($"execution time {Duration}");

            return sb.ToString().TrimEnd();
        }
    }
}
