using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Utils;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public partial class MyProceduralStationModule
    {
        public class MyLoadingConstruction : MyProceduralObject
        {
            public readonly MyProceduralConstructionSeed Seed;

            private readonly FastResourceLock m_creationQueueSemaphore;
            private bool m_creationQueued;
            private MyProceduralConstruction m_construction;
            private MyConstructionCopy m_grids;
            private MyProceduralGridComponent m_component;

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
            public new MyProceduralStationModule Module => base.Module as MyProceduralStationModule;
            private LinkedListNode<MyLoadingConstruction> m_dirtyNode;
            private readonly Vector4I m_cell;

            public MyLoadingConstruction(MyProceduralStationModule module, Vector4I cell, MyProceduralConstructionSeed seed) : base(module)
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
                    var station = x as MyLoadingConstruction;
                    if (station == null) return;
                    station.TimeRemoved = DateTime.UtcNow;
                    SessionCore.Log("Marking entity for removal!");
                };
            }

            private bool Stage_Generate()
            {
                SessionCore.Log("Generation stage for {0}", m_cell);
                m_construction = null;
//                var success = MyGenerator.GenerateFully(Seed, ref m_construction);
//                if (success && !IsMarkedForRemoval)
//                    return true;
//                if (!success) SessionCore.Log("Generation stage failed for {0}", m_cell);
                m_grids = null;
                m_component = null;
                return false;
            }

            private bool Stage_Build()
            {
                SessionCore.Log("Build stage for {0}", m_cell);
                m_grids = MyGridCreator.RemapAndBuild(m_construction);
                if (m_grids == null) SessionCore.Log("Build stage failed for {0}", m_cell);
                if (m_grids != null && !IsMarkedForRemoval) return true;
                m_component = null;
                return false;
            }
            private void Stage_SpawnGrid()
            {
                SessionCore.Log("Spawn stage for {0}", m_cell);
                m_component = m_grids.SpawnAsync();
                if (m_component == null)
                    SessionCore.Log("Spawn stage failed for {0}", m_cell);

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
                    MyPriorityParallel.StartBackground(() =>
                    {
                        if (!Stage_Generate()) return;
                        if (!Stage_Build()) return;
                        MyPriorityParallel.InvokeOnGameThread(Stage_SpawnGrid);
                    });
                }
                else if (m_grids == null)
                {
                    MyPriorityParallel.StartBackground(() =>
                    {
                        if (!Stage_Build()) return;
                        MyPriorityParallel.InvokeOnGameThread(Stage_SpawnGrid);
                    });
                }
                else if (m_component == null)
                {
                    MyPriorityParallel.InvokeOnGameThread(Stage_SpawnGrid);
                }
                else if (m_component.IsConcealed)
                {
                    MyPriorityParallel.InvokeOnGameThread(() =>
                    {
                        m_component.IsConcealed = false;
                        using (m_creationQueueSemaphore.AcquireExclusiveUsing())
                            m_creationQueued = false;
                    });
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
                if (dt > Settings.Instance.StationConcealPersistence && m_component != null && !m_component.IsConcealed)
                    m_component.IsConcealed = true;
                if (dt > Settings.Instance.StationEntityPersistence && m_component != null)
                {
                    removedEntities++;
                    var grids = new List<IMyCubeGrid>(m_component.GridsInGroup);
                    foreach (var grid in grids)
                        grid.Close();
                    m_component = null;
                }
                if (dt > Settings.Instance.StationObjectBuilderPersistence && m_grids != null)
                {
                    removedOB++;
                    m_grids = null;
                }
                // ReSharper disable once InvertIf
                if (dt > Settings.Instance.StationRecipePersistence && m_construction != null)
                {
                    removedRecipe++;
                    m_construction = null;
                }
                return dt > Settings.Instance.StationRecipePersistence;
            }
        }
    }
}