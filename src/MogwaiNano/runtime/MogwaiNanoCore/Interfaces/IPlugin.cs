using System.Collections;

namespace MogwaiNano.Interfaces
{
    public interface IPlugin
    {
        string Name { get; }

        string Description { get; }

        Hashtable Primitives { get; }

        ArrayList Errors { get; }

        void Initialize(MogwaiNano.Engine.MogwaiNanoEngine engine);

        void CleanUp(int engineId);
    }
}
