using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public partial class ProceduralStationModule
    {
        public class LoadingConstruction : ProceduralObject
        {
            public readonly ProceduralConstructionSeed Seed;

            private readonly FastResourceLock m_creationQueueSemaphore;
            private bool m_creationQueued;
            private ProceduralConstruction m_construction;
            private ConstructionCopy m_grids;
            private ProceduralGridComponent m_component;

            private DateTime? m_timeRemovedBack;
            private DateTime? TimeRemoved
            {
                get { return m_timeRemovedBack; }
                set
                {
                    m_timeRemovedBack = value;
                    if (value.HasValue)
                    {
                        if (m_dirtyNode != null) return;
                        m_dirtyNode = Module.m_dirtyInstances.AddLast(this);
                    }
                    else if (m_dirtyNode != null)
                        Module.m_dirtyInstances.Remove(m_dirtyNode);
                }
            }
            public new ProceduralStationModule Module => base.Module as ProceduralStationModule;
            private LinkedListNode<LoadingConstruction> m_dirtyNode;
            private readonly Vector4I m_cell;

            public LoadingConstruction(ProceduralStationModule module, Vector4I cell, ProceduralConstructionSeed seed) : base(module)
            {
                m_cell = cell;
                m_boundingBox = module.StationNoise.GetNodeAABB(cell);
                RaiseMoved();
                Seed = seed;

                m_creationQueued = false;
                m_creationQueueSemaphore = new FastResourceLock();

                m_construction = null;
                m_grids = null;
                m_component = null;
                base.OnRemoved += (x) =>
                {
                    var station = x as LoadingConstruction;
                    if (station == null) return;
                    station.TimeRemoved = DateTime.UtcNow;
                    Module.Debug("Marking station entity for removal!");
                };
            }

            private bool Stage_Generate()
            {
                Module.Debug("Generation stage for {0}/{1}", m_cell, Seed.Seed);
                m_construction = null;

                var success = Module.Generator.GenerateFromSeed(Seed, ref m_construction);
                if (success)
                {
                    Module.Debug("Generation stage success for {0}/{1}", m_cell, Seed.Seed);
                    if (!IsMarkedForRemoval)
                        return true;
                }
                else
                    Module.Error("Generation stage failed for {0}/{1}", m_cell, Seed.Seed);
                m_grids = null;
                m_component = null;
                return false;
            }

            private bool Stage_Build()
            {
                Module.Debug("Build stage for {0}/{1}", m_cell, Seed.Seed);
                m_grids = GridCreator.RemapAndBuild(m_construction);
                if (m_grids != null)
                {
                    Module.Debug("Build stage success for {0}/{1}", m_cell, Seed.Seed);
                    if (!IsMarkedForRemoval)
                        return true;
                }
                else
                    Module.Warning("Build stage failed for {0}/{1}", m_cell, Seed.Seed);
                m_component = null;
                return false;
            }
            private void Stage_SpawnGrid()
            {
                Module.Debug("Spawn stage for {0}/{1}", m_cell, Seed.Seed);
                if (m_grids.IsRegionEmpty())
                {
                    m_component = m_grids.SpawnAsync();
                    if (m_component == null)
                        Module.Warning("Spawn stage failed for {0}/{1}", m_cell, Seed.Seed);
                    else
                        Module.Debug("Spawn stage success for {0}/{1}", m_cell, Seed.Seed);
                }
                else
                {
                    Module.Debug("Spawn stage ignored for {0}/{1}; entities in box", m_cell, Seed.Seed);
                }
                using (m_creationQueueSemaphore.AcquireExclusiveUsing())
                    m_creationQueued = false;
            }

            public void EnsureGenerationStarted()
            {
                TimeRemoved = null;
                using (m_creationQueueSemaphore.AcquireSharedUsing())
                    if (m_creationQueued)
                        return;
                using (m_creationQueueSemaphore.AcquireExclusiveUsing())
                {
                    if (m_creationQueued) return;
                    m_creationQueued = true;
                }
                if (IsMarkedForRemoval) return;
                if (m_construction == null)
                {
                    MyAPIGateway.Parallel.StartBackground(ParallelUtilities.WrapAction(() =>
                    {
                        if (!Stage_Generate()) return;
                        if (!Stage_Build()) return;
                        MyAPIGateway.Utilities.InvokeOnGameThread(ParallelUtilities.WrapAction(Stage_SpawnGrid, Module));
                    }, Module));
                }
                else if (m_grids == null)
                {
                    MyAPIGateway.Parallel.StartBackground(ParallelUtilities.WrapAction(() =>
                    {
                        if (!Stage_Build()) return;
                        MyAPIGateway.Utilities.InvokeOnGameThread(ParallelUtilities.WrapAction(Stage_SpawnGrid, Module));
                    }, Module));
                }
                else if (m_component == null)
                {
                    MyAPIGateway.Utilities.InvokeOnGameThread(ParallelUtilities.WrapAction(Stage_SpawnGrid, Module));
                }
                else
                    m_creationQueued = false;
            }

            public bool IsMarkedForRemoval => TimeRemoved.HasValue;

            internal bool TickRemoval(ref int hiddenEntities, ref int removedEntities, ref int removedOB, ref int removedRecipe)
            {
                if (!TimeRemoved.HasValue) return false;
                using (m_creationQueueSemaphore.AcquireSharedUsing())
                    if (m_creationQueued)
                        return false;
                var dt = DateTime.UtcNow - TimeRemoved;
                if (dt > Module.ConfigReference.StationEntityPersistence && m_component != null)
                {
                    removedEntities++;
                    var grids = new List<IMyCubeGrid>(m_component.GridsInGroup);
                    foreach (var grid in grids)
                        grid.Close();
                    m_component = null;
                }
                if (dt > Module.ConfigReference.StationObjectBuilderPersistence && m_grids != null)
                {
                    removedOB++;
                    m_grids = null;
                }
                // ReSharper disable once InvertIf
                if (dt > Module.ConfigReference.StationRecipePersistence && m_construction != null)
                {
                    removedRecipe++;
                    m_construction = null;
                }
                return dt > Module.ConfigReference.StationRecipePersistence;
            }
        }
    }
}