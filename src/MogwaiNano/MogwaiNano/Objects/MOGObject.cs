using MogwaiNano.Engine;
using System;

namespace MogwaiNano.Objects
{
    public abstract class MOGObject
    {
        public MogwaiNanoEngine Engine { get; set; }

        public bool AutoEval { get; set; }  

        public MOGType Type { get; set; }

        public MOGObject(MogwaiNanoEngine engine, MOGType type)
        {
            Engine = engine;
            Type = type;    
        }

        public abstract MOGObject Clone();

        public virtual void UpdateFromOther(MOGObject other)
        {
            AutoEval = other.AutoEval;  
        }

        public virtual EvalResult EngineEval()
        {
            Engine.StackPush(this);
            return EvalResult.NoError;
        }

        public virtual EvalResult UserEval()
        {
            return EngineEval();
        }
    }
}
