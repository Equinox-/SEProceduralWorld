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
        public const bool DebugDraw = false;
        public const bool DebugDrawReserved = false;
        public const bool DebugDrawBlocks = false;
        public const bool DebugDrawReservedTotal = false;
        public const bool DebugDrawMountPoints = false;
        public const bool DebugDrawRoomColors = false;

        public const bool DebugGenerationStages = true;
        public const bool DebugGenerationResults = true;
        public const bool DebugGenerationResultsError = true;
        public const bool DebugGenerationStagesWeights = false;
        public const bool DebugRoomRemapProfiling = true;

        public const bool AllowAuxillaryGrids = true;
        public const bool ParallelTracing = false;
        public const bool ParallelCatchErrors = true;
    }
}
