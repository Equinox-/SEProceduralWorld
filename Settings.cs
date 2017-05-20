using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcBuild
{
    public class Settings
    {
        public static Settings Instance => SessionCore.Instance?.Settings;

        public bool DebugModuleAABB = true;
        public bool DebugModuleReservedAABB = false;

        public bool DebugGenerationStages = false;
        public bool DebugGenerationResults = false;
        public bool DebugGenerationStagesWeights = false;

        // (10000 km)^3 cells.
        public double FactionDensity = 10e6;
        // There will be roughly (1<<FactionShiftBase) factions per cell.
        public int FactionShiftBase = 2;
        // Pockets of ore concentration last roughly this long
        public double OreMapDensity = 10e3; 

        // This is (within */2) of the minimum distance stations are apart.  Keep low for performance reasons.
        public double StationMinSpacing = 5e3;
        // This is the maximum distance stations are apart.
        public double StationMaxSpacing = 100e3;
    }
}
