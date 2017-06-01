using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public class MyProceduralStationModule : IMyProceduralModule
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
                        m_dirtyNode = m_module.m_dirtyInstances.AddLast(this);
                    }
                    else if (m_dirtyNode != null)
                        m_module.m_dirtyInstances.Remove(m_dirtyNode);
                }
            }
            private LinkedListNode<MyLoadingConstruction> m_dirtyNode;

            private readonly MyProceduralStationModule m_module;
            private readonly Vector4I m_cell;

            public MyLoadingConstruction(MyProceduralStationModule module, Vector4I cell, MyProceduralConstructionSeed seed)
            {
                m_module = module;
                m_cell = cell;
                m_boundingBox = MyProceduralWorld.Instance.StationNoise.GetNodeAABB(cell);
                RaiseMoved();
                Seed = seed;

                m_creationQueued = false;
                m_creationQueueSemaphore = new FastResourceLock();

                m_construction = null;
                m_grids = null;
                m_component = null;
            }

            private bool Stage_Generate()
            {
                SessionCore.Log("Generation stage for {0}", m_cell);
                m_construction = null;
                var success = MyGenerator.GenerateFully(Seed, ref m_construction);
                if (success && !IsMarkedForRemoval)
                    return true;
                if (!success) SessionCore.Log("Generation stage failed for {0}", m_cell);
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
                m_component = m_grids.Spawn();
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

            public override void OnRemove()
            {
                TimeRemoved = DateTime.UtcNow;
                SessionCore.Log("Marking entity for removal!");
            }

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

        private readonly Dictionary<Vector4I, MyLoadingConstruction> m_instances = new Dictionary<Vector4I, MyLoadingConstruction>();
        private readonly LinkedList<MyLoadingConstruction> m_dirtyInstances = new LinkedList<MyLoadingConstruction>();

        public MyLoadingConstruction InstanceAt(Vector4I octreeNode)
        {
            return m_instances.GetValueOrDefault(octreeNode);
        }

        public MyLoadingConstruction InstanceAt(Vector3D worldPosition)
        {
            return InstanceAt(MyProceduralWorld.Instance.StationNoise.GetOctreeNodeAt(worldPosition));
        }

        public IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
        {
            var aabb = new BoundingBoxD(include.Center - include.Radius, include.Center + include.Radius);
            foreach (var cell in MyProceduralWorld.Instance.StationNoise.TryGetSpawnIn(aabb, (x) => include.Intersects(x) && (!exclude.HasValue || exclude.Value.Contains(x) != ContainmentType.Contains)))
            {
                MyLoadingConstruction instance;
                if (!m_instances.TryGetValue(cell.Item1, out instance))
                {
                    instance = m_instances[cell.Item1] = new MyLoadingConstruction(this, cell.Item1,
                        new MyProceduralConstructionSeed(cell.Item2, null, cell.Item1.GetHashCode()));
                }
                else if (!instance.IsMarkedForRemoval)
                    continue; // Already loaded + not marked for removal -- already in the tree.

                instance.EnsureGenerationStarted();
                yield return instance;
            }
        }

        public void UpdateBeforeSimulation()
        {
            int hiddenEntities = 0, removedEntities = 0, removedOBs = 0, removedRecipes = 0;
            foreach (var instance in m_dirtyInstances)
            {
                if (instance.TickRemoval(ref hiddenEntities, ref removedEntities, ref removedOBs, ref removedRecipes))
                {
                    // Remove from list
                }
            }
            if (removedEntities != 0 || removedOBs != 0 || removedRecipes != 0 || hiddenEntities != 0)
                SessionCore.Log("Procedural station module hide {3} station entities, removed {0} station entities, {1} object builders, and {2} recipes", removedEntities, removedOBs, removedRecipes, hiddenEntities);
        }
    }
}
