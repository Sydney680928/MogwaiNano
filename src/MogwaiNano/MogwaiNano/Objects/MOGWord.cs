using MogwaiNano.Engine;

namespace MogwaiNano.Objects
{
    public class MOGWord : MOGObject
    {
        public string Value { get; set; }
        
        public MOGWord(MogwaiNanoEngine engine, string value) : base(engine, engine.TypeWord)
        {
            Value = value;
        }

        public override EvalResult EngineEval()
        {
            // This word is a function ?

            var func = Engine.GetFunction(Value);

            if (func != null)
                return func.Execute();

            // This word is a var ?

            var value = Engine.VarRead(Value);

            if (value == null)
                return EvalResult.Failure(Engine, Error.UnknownWordError, Value);

            Engine.StackPush(value);

            return EvalResult.NoError;
        }

        public override MOGObject Clone()
        {
            var obj = new MOGWord(Engine, Value);
            obj.UpdateFromOther(this);
            return obj;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
