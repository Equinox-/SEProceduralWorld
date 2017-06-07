using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Voxels;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public static class MyVoxelUtility
    {
        public static MyCompositeShapeProviderBuilder CreateProceduralAsteroidProvider(int seed, float radius, float deviationScale)
        {
            return MyCompositeShapeProviderBuilder.CreateAsteroidShape(seed, radius, 0);
        }
        public static MyCompositeShapeProviderBuilder CreateProceduralAsteroidProvider(int seed, float radius)
        {
            return MyCompositeShapeProviderBuilder.CreateAsteroidShape(seed, radius, 2);
        }

        // MyEntityIdentifier.ID_OBJECT_TYPE.ASTEROID
        private const int ASTEROID_TYPE = 6;
        private static long GetAsteroidEntityId(string storageName)
        {
            return ((long)storageName.Hash64()) & 0x00FFFFFFFFFFFFFF | ((long)ASTEROID_TYPE << 56);
        }

        public static IMyVoxelMap SpawnAsteroid(MyPositionAndOrientation pos, MyCompositeShapeProviderBuilder provider)
        {
            var storage = new MyOctreeStorageBuilder(provider, MyVoxelCoordSystems.FindBestOctreeSize(provider.Size));
            var storageName = $"proc_astr_{provider.Seed}_{provider.Size}_{(long)pos.Position.X}_{(long)pos.Position.Y}_{(long)pos.Position.Z}";
            var entityID = GetAsteroidEntityId(storageName);
            IMyEntity currEntity;
            if (MyAPIGateway.Entities.TryGetEntityById(entityID, out currEntity))
                return currEntity as IMyVoxelMap;
            var data = storage.GetCompressedData();

            var storageInstance = MyAPIGateway.Session.VoxelMaps.CreateStorage(data);
            var entity = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(storageName, storageInstance, pos.Position, entityID);
            entity.Save = false;
            var realEntity = entity as MyEntity;
            if (realEntity == null) return entity;
            MyEntities.RaiseEntityCreated(realEntity);
            MyEntities.Add(realEntity);
            realEntity.IsReadyForReplication = true;
            return entity;
        }

        public static int FindAsteroidSeed(int seed, float size, HashSet<MyDefinitionId> prohibitsOre, HashSet<MyDefinitionId> requiresOre, int maxTries = 10)
        {
            if (requiresOre.Count == 0 && prohibitsOre.Count == 0) return seed;
            var gen = MyAsteroidShapeGenerator.AsteroidGenerators[MyAPIGateway.Session.SessionSettings.VoxelGeneratorVersion];
            for (var i = 0; i < maxTries; i++)
            {
                MyAsteroidShapeGenerator.MyCompositeShapeGeneratedDataBuilder data;
                gen(seed, size, out data);
                if (requiresOre.Count == 0 || requiresOre.Contains(data.DefaultMaterial.Id) || data.Deposits.Any(x => requiresOre.Contains(x.Material.Id)))
                    if (prohibitsOre.Count == 0 || (!prohibitsOre.Contains(data.DefaultMaterial.Id) && !data.Deposits.Any(x => prohibitsOre.Contains(x.Material.Id))))
                        break;
                seed *= 982451653;
            }
            return seed;
        }
    }
}
