using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.Voxels;
using VRageMath;
using VRageRender.Messages;
using IMyStorage = VRage.ModAPI.IMyStorage;

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
        private const int PLANET_TYPE = 7;
        private static long GetAsteroidEntityId(string storageName)
        {
            return ((long)storageName.Hash64()) & 0x00FFFFFFFFFFFFFF | ((long)ASTEROID_TYPE << 56);
        }

        private static long GetPlanetEntityId(string storageName)
        {
            return ((long)storageName.Hash64()) & 0x00FFFFFFFFFFFFFF | ((long)PLANET_TYPE << 56);
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

        private static void CastProhibit<R>(object o, out R res) where R : class
        {
            res = (R) o;
        }

        public static MyPlanet SpawnPlanet(Vector3D pos, MyPlanetGeneratorDefinition generatorDef, long seed, float size)
        {
            var provider = new MyPlanetStorageProviderBuilder();
            provider.Init(seed, generatorDef, size / 2f);

            var storageBuilder = new MyOctreeStorageBuilder(provider, provider.StorageSize);
            var storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(storageBuilder.GetCompressedData());

            var minHillSize = provider.Radius * generatorDef.HillParams.Min;
            var maxHillSize = provider.Radius * generatorDef.HillParams.Max;

            var averagePlanetRadius = provider.Radius;

            var outerRadius = averagePlanetRadius + maxHillSize;
            var innerRadius = averagePlanetRadius + minHillSize;

//            var atmosphereRadius = generatorDef.AtmosphereSettings.HasValue &&
//                generatorDef.AtmosphereSettings.Value.Scale > 1f ? 1 + generatorDef.AtmosphereSettings.Value.Scale : 1.75f;
            var atmosphereRadius = 1.75f;
            atmosphereRadius *= (float)provider.Radius;

            var random = new Random((int)seed);
            var redAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.R.Min, generatorDef.HostileAtmosphereColorShift.R.Max);
            var greenAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.G.Min, generatorDef.HostileAtmosphereColorShift.G.Max);
            var blueAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.B.Min, generatorDef.HostileAtmosphereColorShift.B.Max);

            var atmosphereWavelengths = new Vector3(0.650f + redAtmosphereShift, 0.570f + greenAtmosphereShift, 0.475f + blueAtmosphereShift);

            atmosphereWavelengths.X = MathHelper.Clamp(atmosphereWavelengths.X, 0.1f, 1.0f);
            atmosphereWavelengths.Y = MathHelper.Clamp(atmosphereWavelengths.Y, 0.1f, 1.0f);
            atmosphereWavelengths.Z = MathHelper.Clamp(atmosphereWavelengths.Z, 0.1f, 1.0f);

            var storageName = $"proc_planet_{provider.Seed}_{(int)provider.Radius}_{(long)pos.X}_{(long)pos.Y}_{(long)pos.Z}";
            var planet = new MyPlanet();
            planet.EntityId = GetPlanetEntityId(storageName);
            MyPlanetInitArguments planetInitArguments = new MyPlanetInitArguments();
            planetInitArguments.StorageName = storageName;
            CastProhibit(storage, out planetInitArguments.Storage);
            var posMinCorner = pos - provider.Radius;
            planetInitArguments.PositionMinCorner = posMinCorner;
            planetInitArguments.Radius = (float)provider.Radius;
            planetInitArguments.AtmosphereRadius = atmosphereRadius;
            planetInitArguments.MaxRadius = (float)outerRadius;
            planetInitArguments.MinRadius = (float)innerRadius;
            planetInitArguments.HasAtmosphere = generatorDef.HasAtmosphere;
            planetInitArguments.AtmosphereWavelengths = atmosphereWavelengths;
            planetInitArguments.GravityFalloff = generatorDef.GravityFalloffPower;
            planetInitArguments.MarkAreaEmpty = true;
//            planetInitArguments.AtmosphereSettings = generatorDef.AtmosphereSettings.HasValue ? generatorDef.AtmosphereSettings.Value : MyAtmosphereSettings.Defaults();
            planetInitArguments.SurfaceGravity = generatorDef.SurfaceGravity;
            planetInitArguments.AddGps = false;
            planetInitArguments.SpherizeWithDistance = true;
            planetInitArguments.Generator = generatorDef;
            planetInitArguments.UserCreated = false;
            planetInitArguments.InitializeComponents = true;

            planet.Init(planetInitArguments);

            MyEntities.Add(planet);
            MyEntities.RaiseEntityCreated(planet);

            planet.IsReadyForReplication = true;

            return planet;
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
