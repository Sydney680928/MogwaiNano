using System;
using System.Diagnostics;
using System.Threading;

using MogwaiNano.Engine;
using MogwaiNano.Objects;


namespace TestMogwaiNanoADC
{
    public class Program
    {
        public static void Main()
        {
            var engine = new MogwaiNanoEngine("TEST ADC");
            var plugin = new MogwaiNanoADC.MogwaiNanoADC();

            plugin.Initialize(engine);

            foreach (var key in plugin.Primitives.Keys)
            {
                var value = plugin.Primitives[key] as MogwaiNanoEngine.PrimitiveDelegate;
                MogwaiNanoEngine.Primitives[key] = value;
            }

            var r = engine.Run("'X' 1 adc2.open 'X' adc2.read debug.vs.write 'X' adc2.close 1 adc2.maxValue debug.vs.write 1 adc2.resolutionInBits debug.vs.write");
     
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
