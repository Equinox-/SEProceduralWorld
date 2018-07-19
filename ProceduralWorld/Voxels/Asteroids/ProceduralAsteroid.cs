using System.Collections.Generic;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Voxels.VoxelBuilder;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public partial class AsteroidFieldModule
    {
        public struct SpawnRequest
        {
            public ProceduralAsteroid Asteroid;
            public double Distance;
        }


        public class SpawnRequestComparer : IComparer<SpawnRequest>
        {
            public static readonly SpawnRequestComparer Instance = new SpawnRequestComparer();

            public int Compare(SpawnRequest x, SpawnRequest y)
            {
                if (x.Distance > y.Distance + 1)
                    return -1;
                else if (y.Distance > x.Distance + 1)
                    return 1;
                if (x.Asteroid == y.Asteroid)
                    return 0;
                return x.Asteroid.GetHashCode() > y.Asteroid.GetHashCode() ? 1 : -1;
            }
        }

        public class ProceduralAsteroid : ProceduralObject
        {
            private Vector4I Seed { get; }
            private Vector3D WorldPosition { get; }
            private float Size { get; }
            private IMyVoxelMap VoxelMap { get; set; }
            private AsteroidLayer SeedSpecs { get; }
            private Quaternion Rotation { get; }
            private new AsteroidFieldModule Module => base.Module as AsteroidFieldModule;

            public ProceduralAsteroid(AsteroidFieldModule field, Vector4I seed, Vector3D worldPos, double size, AsteroidLayer layer) : base(field)
            {
                Seed = seed;
                WorldPosition = worldPos;
                Rotation = new Quaternion((float) (worldPos.X % 1), (float) (worldPos.Y % 1), (float) (worldPos.Z % 1), 1);
                Rotation.Normalize();
                Size = (float) size;
                m_boundingBox = new BoundingBoxD(WorldPosition - Size, WorldPosition + Size);
                SeedSpecs = layer;
                RaiseMoved();
                base.OnRemoved += HandleRemove;
            }

            private static void MarkForSave(IMyEntity e)
            {
                e.OnPhysicsChanged -= MarkForSave;
                e.Save = true;
            }

            public void SpawnIfNeeded(double observedDistance)
            {
                if (VoxelMap != null) return;
                lock (this)
                {
                    if (!m_spawnQueued)
                    {
                        var item = new SpawnRequest() {Asteroid = this, Distance = observedDistance};
                        Module.m_asteroidsToAdd.Insert(item, item);
                    }

                    m_removeQueued = false;
                    m_spawnQueued = true;
                }
            }

            private static void HandleRemove(ProceduralObject obj)
            {
                var ast = obj as ProceduralAsteroid;
                if (ast?.VoxelMap == null || ast.VoxelMap.Save) return;
                lock (ast)
                {
                    if (!ast.m_removeQueued)
                        ast.Module.m_asteroidsToRemove.Enqueue(ast);
                    ast.m_spawnQueued = false;
                    ast.m_removeQueued = true;
                }
            }

            private bool m_spawnQueued = false;

            internal void ExecuteSpawn()
            {
                lock (this)
                {
                    if (m_removeQueued || !m_spawnQueued) return;
                    if (VoxelMap == null)
                    {
                        var mat = MatrixD.CreateFromQuaternion(Rotation);
                        mat.Translation = WorldPosition;
                        var genSeed = WorldPosition.GetHashCode();
                        if (VoxelUtility.TryFindAsteroidSeed(ref genSeed, Size, SeedSpecs.ProhibitsOre, SeedSpecs.RequiresOre, 10) || !SeedSpecs.ExcludeInvalid)
                        {
                            VoxelMap = VoxelUtility.SpawnAsteroid(new MyPositionAndOrientation(mat),
                                VoxelUtility.CreateProceduralAsteroidProvider(genSeed, Size));
                            VoxelMap.OnPhysicsChanged += MarkForSave;
                        }
                    }

                    m_spawnQueued = false;
                    m_removeQueued = false;
                }
            }

            private bool m_removeQueued = false;

            internal void ExecuteRemove()
            {
                lock (this)
                {
                    if (m_spawnQueued || !m_removeQueued) return;
                    if (VoxelMap != null)
                    {
                        MyAPIGateway.Entities.RemoveEntity(VoxelMap);
                        var vox = VoxelMap;
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => vox?.Close());
                    }

                    VoxelMap = null;
                    m_removeQueued = false;
                    m_removeQueued = false;
                }
            }
        }
    }
}