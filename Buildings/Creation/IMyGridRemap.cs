using System.Collections.Generic;
using System.Diagnostics;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public interface IMyGridRemap
    {
        void Remap(MyObjectBuilder_CubeGrid grid);
        void Reset();
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
            if (Settings.Instance.DebugRoomRemapProfiling)
                SessionCore.Log("Remap module {0} ran on {1} grids in {2}", remap.GetType().Name, count, watch.Elapsed);
        }
    }
}
