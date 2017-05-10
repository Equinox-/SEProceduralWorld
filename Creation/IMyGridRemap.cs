using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;

namespace ProcBuild.Creation
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
            foreach (var grid in grids)
                remap.Remap(grid);
            remap.Reset();
        }
    }
}
