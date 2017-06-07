using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Manager;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public partial class MyProceduralStationModule : MyProceduralModule
    {
        private readonly Dictionary<Vector4I, MyLoadingConstruction> m_instances = new Dictionary<Vector4I, MyLoadingConstruction>(Vector4I.Comparer);
        private readonly LinkedList<MyLoadingConstruction> m_dirtyInstances = new LinkedList<MyLoadingConstruction>();

        public override bool RunOnClients => false;

        public MyLoadingConstruction InstanceAt(Vector4I octreeNode)
        {
            return m_instances.GetValueOrDefault(octreeNode);
        }

        public MyLoadingConstruction InstanceAt(Vector3D worldPosition)
        {
            return InstanceAt(MyProceduralWorld.Instance.StationNoise.GetOctreeNodeAt(worldPosition));
        }

        public override IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
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

        public override void UpdateBeforeSimulation(TimeSpan maxTime)
        {
            int hiddenEntities = 0, removedEntities = 0, removedOBs = 0, removedRecipes = 0;

            var node = m_dirtyInstances.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.TickRemoval(ref hiddenEntities, ref removedEntities, ref removedOBs, ref removedRecipes))
                    m_dirtyInstances.Remove(node);
                node = next;
            }
            if (removedEntities != 0 || removedOBs != 0 || removedRecipes != 0 || hiddenEntities != 0)
                Log(MyLogSeverity.Debug, "Procedural station module hide {3} station entities, removed {0} station entities, {1} object builders, and {2} recipes", removedEntities, removedOBs, removedRecipes, hiddenEntities);
        }
    }
}
