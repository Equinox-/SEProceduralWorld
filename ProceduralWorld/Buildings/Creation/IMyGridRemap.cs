using System.Collections.Generic;
using System.Diagnostics;
using BuffPanel.Logging;
using Equinox.Utils.Logging;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public abstract class IMyGridRemap
    {
        public readonly IMyLogging Logger;

        protected IMyGridRemap(IMyLoggingBase root)
        {
            Logger = root.CreateProxy(GetType().Name);
        }

        public abstract void Remap(MyObjectBuilder_CubeGrid grid);
        public abstract void Reset();
    }

    public static class MyGridRemapExtensions
    {
        public static void RemapAndReset(this IMyGridRemap remap, IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            var watch = new Stopwatch();
            watch.Restart();
            var count = 0;
            foreach (var grid in grids)
            {
                remap.Remap(grid);
                count++;
            }
            remap.Reset();
            remap.Logger.Debug("Ran on {0} grids in {1}", count, watch.Elapsed);
        }
    }
}
