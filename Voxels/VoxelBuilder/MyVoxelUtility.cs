using Sandbox.ModAPI;
using VRage;
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
            long hash = 5381;
            // djb2 (http://www.cse.yorku.ca/~oz/hash.html)
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var t in storageName)
                hash = ((hash << 5) + hash) + (long)t;
            return hash & 0x00FFFFFFFFFFFFFF | ((long)ASTEROID_TYPE << 56);
        }
        
        public static IMyVoxelMap SpawnAsteroid(MyPositionAndOrientation pos, MyCompositeShapeProviderBuilder provider)
        {
            var storage = new MyOctreeStorageBuilder(provider, MyVoxelCoordSystems.FindBestOctreeSize(provider.Size));
            var storageName = $"proc_astr_{provider.Seed}_{provider.Size}_{(long) pos.Position.X}_{(long) pos.Position.Y}_{(long) pos.Position.Z}";
            var entityID = GetAsteroidEntityId(storageName);
            IMyEntity currEntity;
            if (MyAPIGateway.Entities.TryGetEntityById(entityID, out currEntity))
                return currEntity as IMyVoxelMap;
            var data = storage.GetCompressedData();

            var storageInstance = MyAPIGateway.Session.VoxelMaps.CreateStorage(data);
            var entity = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(storageName, storageInstance, pos.Position, entityID);
            entity.Save = false;
            return entity;
        }
    }
}
