using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace MogwaiNano.Interfaces
{
    public interface IPlugin
    {
        string Name { get; }

        string Description { get; }

        Hashtable Primitives { get; }

        ArrayList Errors { get; }

        void Initialize( MogwaiNano.Engine.MogwaiNanoEngine engine);

        void CleanUp(int engineId);
    }
}
