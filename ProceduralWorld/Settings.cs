using System;
using System.Threading;
using ParallelTasks;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Collections;

namespace Equinox.ProceduralWorld
{
    public class Settings
    {
        public static Settings Instance => SessionCore.Instance?.Settings;

        public bool DebugDraw = false;
        public bool DebugDrawReserved = false;
        public bool DebugDrawBlocks = false;
        public bool DebugDrawReservedTotal = false;
        public bool DebugDrawMountPoints = false;
        public bool DebugDrawRoomColors = false;

        public bool DebugGenerationStages = true;
        public bool DebugGenerationResults = true;
        public bool DebugGenerationResultsError = true;
        public bool DebugGenerationStagesWeights = false;
        public bool DebugRoomRemapProfiling = true;

        // Pockets of ore concentration last roughly this long
        public double OreMapDensity = 10e3;

        // This is (within */2) of the minimum distance stations encounters are apart.  Keep high for performance reasons.
        // For context, 250e3 for Earth-Moon, 2300e3 for Earth-Mars, 6000e3 for Earth-Alien
        public double StationMinSpacing = 100e3;
        // This is the maximum distance station encounters are apart.
        public double StationMaxSpacing = 1000e3;

        // Procedural Station Management
        /// <summary>
        /// Time to keep a procedural station inside the scene graph after it's no longer visible.
        /// </summary>
        public TimeSpan StationConcealPersistence = TimeSpan.FromSeconds(30);
        /// <summary>
        /// Time to keep a procedural station entity allocated after it's no longer visible.
        /// </summary>
        public TimeSpan StationEntityPersistence = TimeSpan.FromMinutes(3);
        /// <summary>
        /// Time to keep the object builder for a station object in memory after it's no longer visible.
        /// </summary>
        public TimeSpan StationObjectBuilderPersistence = TimeSpan.FromMinutes(5);
        /// <summary>
        /// Time to keep the high level structure of a station in memory after it's no longer visible.
        /// </summary>
        public TimeSpan StationRecipePersistence = TimeSpan.FromMinutes(15);
    }
}
