using MogwaiNano.Engine;
using MogwaiNano.Objects;
using nanoFramework.Runtime.Native;
using System;

namespace MogwaiNanoDateTime
{
    public class DateTimeManager
    {
        public static EvalResult PrimitiveSetDateTime(MogwaiNanoEngine engine, string name)
        {
            // [year: 2026 month: 5 day: 3 hour: 14 minute: 30 second: 0] setDateTime

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                var year = record.GetItem("year") as MOGNumber;
                var month = record.GetItem("month") as MOGNumber;
                var day = record.GetItem("day") as MOGNumber;
                var hour = record.GetItem("hour") as MOGNumber;
                var minute = record.GetItem("minute") as MOGNumber;
                var second = record.GetItem("second") as MOGNumber;

                if (year == null || month == null || day == null)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "year:, month: and day: keys are mandatory");

                var yearValue = (int)year.Value;
                var monthValue = (int)month.Value;
                var dayValue = (int)day.Value;

                int hourValue = hour != null ? (int)hour.Value : 0;
                int minuteValue = minute != null ? (int)minute.Value : 0;
                int secondValue = second != null ? (int)second.Value : 0;

                try
                {
                    var dateTime = new DateTime(yearValue, monthValue, dayValue, hourValue, minuteValue, secondValue);
                    Rtc.SetSystemTime(dateTime);
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, ex.Message);
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveNow(MogwaiNanoEngine engine, string name)
        {
            engine.StackPush(new MOGNumber(engine, DateTime.UtcNow.Ticks));
            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveDateToRecord(MogwaiNanoEngine engine, string name)
        {
            // 6.343544 date-> ====> [day: month: year: hour: minute: second: dayOfYear dayOfWeek]

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var ticks = engine.StackPop() as MOGNumber;
                var ticksValue = (long)ticks.DoubleValue;
                var dt = new DateTime(ticksValue);

                var record = new MOGRecord(engine);
                record.SetItem("year", new MOGNumber(engine, dt.Year));
                record.SetItem("month", new MOGNumber(engine, dt.Month));
                record.SetItem("day", new MOGNumber(engine, dt.Day));
                record.SetItem("hour", new MOGNumber(engine, dt.Hour));
                record.SetItem("minute", new MOGNumber(engine, dt.Minute));
                record.SetItem("second", new MOGNumber(engine, dt.Second));
                record.SetItem("dayOfYear", new MOGNumber(engine, dt.DayOfYear));
                record.SetItem("dayOfWeek", new MOGNumber(engine, (int)dt.DayOfWeek));

                engine.StackPush(record);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveRecordToDate(MogwaiNanoEngine engine, string name)
        {
            // [day: month: year: hour: minute: second:] ->date ====> 6.78766

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                var year = record.GetItem("year") as MOGNumber;
                var month = record.GetItem("month") as MOGNumber;
                var day = record.GetItem("day") as MOGNumber;
                var hour = record.GetItem("hour") as MOGNumber;
                var minute = record.GetItem("minute") as MOGNumber;
                var second = record.GetItem("second") as MOGNumber;

                if (year == null || month == null || day == null)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "year:, month: and day: keys are mandatory");

                var yearValue = (int)year.Value;
                var monthValue = (int)month.Value;
                var dayValue = (int)day.Value;

                int hourValue = hour != null ? (int)hour.Value : 0;
                int minuteValue = minute != null ? (int)minute.Value : 0;
                int secondValue = second != null ? (int)second.Value : 0;

                var dateTime = new DateTime(yearValue, monthValue, dayValue, hourValue, minuteValue, secondValue);
                engine.StackPush(new MOGNumber(engine, dateTime.Ticks));
                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveToDuration(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var ticks = engine.StackPop() as MOGNumber;
                var ticksValue = (long)ticks.DoubleValue;

                var ts = new TimeSpan(ticksValue);

                var record = new MOGRecord(engine);
                record.SetItem("days", new MOGNumber(engine, ts.Days));
                record.SetItem("hours", new MOGNumber(engine, ts.Hours));
                record.SetItem("minutes", new MOGNumber(engine, ts.Minutes));
                record.SetItem("seconds", new MOGNumber(engine, ts.Seconds));
                record.SetItem("milliseconds", new MOGNumber(engine, ts.Milliseconds));

                engine.StackPush(record);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveToDurations(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber))
            {
                var ticks = engine.StackPop() as MOGNumber;
                var t = new TimeSpan((long)ticks.DoubleValue);

                var r = new MOGRecord(engine);
                r.SetItem("totalDays", new MOGNumber(engine, t.TotalDays));
                r.SetItem("totalHours", new MOGNumber(engine, t.TotalHours));
                r.SetItem("totalMinutes", new MOGNumber(engine, t.TotalMinutes));
                r.SetItem("totalSeconds", new MOGNumber(engine, t.TotalSeconds));
                r.SetItem("totalMs", new MOGNumber(engine, t.TotalMilliseconds));
                engine.StackPush(r);

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveFromDuration(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                int days = 0;
                int hours = 0;
                int minutes = 0;
                int seconds = 0;
                int milliseconds = 0;

                if (record.GetItem("days") is MOGNumber d)
                    days = (int)d.Value;

                if (record.GetItem("hours") is MOGNumber h)
                    hours = (int)h.Value;

                if (record.GetItem("minutes") is MOGNumber mm)
                    minutes = (int)mm.Value;

                if (record.GetItem("seconds") is MOGNumber ss)
                    seconds = (int)ss.Value;

                if (record.GetItem("ms") is MOGNumber ms)
                    milliseconds = (int)ms.Value;

                try
                {
                    var dt = new TimeSpan(days, hours, minutes, seconds, milliseconds);
                    engine.StackPush(new MOGNumber(engine, dt.Ticks));
                    return EvalResult.NoError;
                }
                catch
                {

                }

                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "day: month: year: keys are mandatories");
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveDateTimeDiff(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length < 2)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);
            
            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var v2 = engine.StackPop() as MOGNumber;
                var v1 = engine.StackPop() as MOGNumber;     
                var diff = v1.DoubleValue - v2.DoubleValue;

                engine.StackPush(new MOGNumber(engine, diff));
                
                return EvalResult.NoError;
            }
            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveDateTimeAdd(MogwaiNanoEngine engine, string name)
        {
            var s = engine.StackSign(2);

            if (s.Length < 2)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGNumber) && s[1] == typeof(MOGNumber))
            {
                var v2 = engine.StackPop() as MOGNumber;
                var v1 = engine.StackPop() as MOGNumber;
                var diff = v1.DoubleValue + v2.DoubleValue;

                engine.StackPush(new MOGNumber(engine, diff));

                return EvalResult.NoError;
            }
            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }
    }
}
