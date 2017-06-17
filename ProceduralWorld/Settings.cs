using System;
using System.Threading;
using ParallelTasks;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Collections;

namespace Equinox.ProceduralWorld
{
    public class Example
    {
        public struct SliceToProcess
        {
            public int ShellIndex;
            public int MaxShellIndex;
            public IMyOreDetector Detector;
        }
        public MyBinaryStructHeap<int, SliceToProcess> m_Heap = new MyBinaryStructHeap<int, SliceToProcess>();

        public void Add(IMyOreDetector detector, int maxShell)
        {
            lock (m_Heap)
                m_Heap.Insert(new SliceToProcess() { ShellIndex = 0, MaxShellIndex = maxShell, Detector = detector }, 0);
        }

        public void TickWorker()
        {
            if (m_Heap.Count > 0)
            {
                SliceToProcess proc;
                lock (m_Heap)
                {
                    proc = m_Heap.RemoveMin();
                    // This can be here if you want multiple workers able to work with a single ore detector on multiple shells
                    if (proc.ShellIndex < proc.MaxShellIndex)
                        m_Heap.Insert(new SliceToProcess() { Detector = proc.Detector, MaxShellIndex = proc.MaxShellIndex, ShellIndex = proc.ShellIndex + 1 }, proc.ShellIndex + 1);
                }

                // process proc

                // Or here if you want only one worker working on an ore detector at a time.
                if (proc.ShellIndex < proc.MaxShellIndex)
                    lock (m_Heap)
                        m_Heap.Insert(new SliceToProcess() { Detector = proc.Detector, MaxShellIndex = proc.MaxShellIndex, ShellIndex = proc.ShellIndex + 1 }, proc.ShellIndex + 1);
            }
        }
    }

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
        public bool DebugRoomRemapProfiling = false;

        // (100 km)^3 cells.
        public double FactionDensity = 1e5;
        // There will be roughly (1<<FactionShiftBase) factions per cell.
        public int FactionShiftBase = 3;
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
