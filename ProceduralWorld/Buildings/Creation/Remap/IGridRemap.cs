using System.Collections.Generic;
using System.Diagnostics;
using Equinox.Utils.Logging;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Creation.Remap
{
    public abstract class IGridRemap
    {
        public readonly ILogging Logger;

        protected IGridRemap(ILoggingBase root)
        {
            Logger = root.CreateProxy(GetType().Name);
        }

        public abstract void Remap(MyObjectBuilder_CubeGrid grid);
        public abstract void Reset();
    }

    public static class GridRemapExtensions
    {
        public static void RemapAndReset(this IGridRemap remap, IEnumerable<MyObjectBuilder_CubeGrid> grids)
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
