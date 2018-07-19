using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Random;
using ProtoBuf;
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
    public static class VoxelUtility
    {
        public static CompositeShapeProviderBuilder CreateProceduralAsteroidProvider(int seed, float radius, float deviationScale)
        {
            return CompositeShapeProviderBuilder.CreateAsteroidShape(seed, radius, 0);
        }

        public static CompositeShapeProviderBuilder CreateProceduralAsteroidProvider(int seed, float radius)
        {
            return CompositeShapeProviderBuilder.CreateAsteroidShape(seed, radius, 2);
        }

        // MyEntityIdentifier.ID_OBJECT_TYPE.ASTEROID
        private const int ASTEROID_TYPE = 6;
        private const int PLANET_TYPE = 7;

        private static long GetAsteroidEntityId(string storageName)
        {
            return ((long) storageName.Hash64()) & 0x00FFFFFFFFFFFFFF | ((long) ASTEROID_TYPE << 56);
        }

        private static long GetPlanetEntityId(string storageName)
        {
            return ((long) storageName.Hash64()) & 0x00FFFFFFFFFFFFFF | ((long) PLANET_TYPE << 56);
        }

        public static IMyVoxelMap SpawnAsteroid(MyPositionAndOrientation pos, CompositeShapeProviderBuilder provider)
        {
            var storage = new OctreeStorageBuilder(provider, MyVoxelCoordSystems.FindBestOctreeSize(provider.Size));
            var storageName = $"proc_astr_{provider.Seed}_{provider.Size}_{(long) pos.Position.X}_{(long) pos.Position.Y}_{(long) pos.Position.Z}";
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
            return entity;
        }

        private static void CastProhibit<TR>(object o, out TR res) where TR : class
        {
            res = (TR) o;
        }

        [ProtoContract]
        public struct MyAtmosphereSettings
        {
            [ProtoMember(9)]
            public Vector3 RayleighScattering;

            [ProtoMember(11)]
            public float MieScattering;

            [ProtoMember(13)]
            public Vector3 MieColorScattering;

            [ProtoMember(16)]
            public float RayleighHeight;

            [ProtoMember(18)]
            public float RayleighHeightSpace;

            [ProtoMember(20)]
            public float RayleighTransitionModifier;

            [ProtoMember(22)]
            public float MieHeight;

            [ProtoMember(24)]
            public float MieG;

            [ProtoMember(26)]
            public float Intensity;

            [ProtoMember(28)]
            public float FogIntensity;

            [ProtoMember(30)]
            public float SeaLevelModifier;

            [ProtoMember(32)]
            public float AtmosphereTopModifier;

            [ProtoMember(35)]
            public float Scale;

            public static MyAtmosphereSettings Defaults()
            {
                MyAtmosphereSettings result;
                result.RayleighScattering = new Vector3(20f, 7.5f, 10f);
                result.MieScattering = 50f;
                result.MieColorScattering = new Vector3(50f, 50f, 50f);
                result.RayleighHeight = 10f;
                result.RayleighHeightSpace = 10f;
                result.RayleighTransitionModifier = 1f;
                result.MieHeight = 50f;
                result.MieG = 0.9998f;
                result.Intensity = 1f;
                result.FogIntensity = 0f;
                result.SeaLevelModifier = 1f;
                result.AtmosphereTopModifier = 1f;
                result.Scale = 0.5f;
                return result;
            }

            public static MyAtmosphereSettings FromKeen<TR>(TR o)
            {
                var data = MyAPIGateway.Utilities.SerializeToBinary(o);
                return MyAPIGateway.Utilities.SerializeFromBinary<MyAtmosphereSettings>(data);
            }

            public TR ToKeen<TR>()
            {
                var data = MyAPIGateway.Utilities.SerializeToBinary(this);
                return MyAPIGateway.Utilities.SerializeFromBinary<TR>(data);
            }
        }

        private static readonly byte[] _defaultAtmosphereSettings =
            Convert.FromBase64String(
                "ShLFAgAAoEHtAgAA8ECVAwAAIEFdAABIQmoSxQIAAEhC7QIAAEhClQMAAEhChQEAACBBlQEAACBBpQEAAIA/tQEAAEhCxQHl8n8/1QEAAIA/9QEAAIA/hQIAAIA/nQIAAAA/");

        private static void MoveAtmosphereSettings<TR>(TR? o, out TR res) where TR : struct
        {
            res = o ?? MyAPIGateway.Utilities.SerializeFromBinary<TR>(_defaultAtmosphereSettings);
        }

        private static float AtmosphereRadius<TR>(TR? data) where TR : struct
        {
            if (data.HasValue)
            {
                var scale = MyAtmosphereSettings.FromKeen(data.Value).Scale;
                if (scale > 1f)
                {
                    return 1 + scale;
                }
            }

            return 1.75f;
        }

        public static MyPlanet SpawnPlanet(Vector3D pos, MyPlanetGeneratorDefinition generatorDef, long seed, float size, string storageName)
        {
            var provider = new PlanetStorageProviderBuilder();
            provider.Init(seed, generatorDef, size / 2f);

            var storageBuilder = new OctreeStorageBuilder(provider, provider.StorageSize);
            var storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(storageBuilder.GetCompressedData());

            var minHillSize = provider.Radius * generatorDef.HillParams.Min;
            var maxHillSize = provider.Radius * generatorDef.HillParams.Max;

            var averagePlanetRadius = provider.Radius;

            var outerRadius = averagePlanetRadius + maxHillSize;
            var innerRadius = averagePlanetRadius + minHillSize;

            var atmosphereRadius = AtmosphereRadius(generatorDef.AtmosphereSettings);
            atmosphereRadius *= (float) provider.Radius;

            var random = new Random((int) seed);
            var redAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.R.Min, generatorDef.HostileAtmosphereColorShift.R.Max);
            var greenAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.G.Min, generatorDef.HostileAtmosphereColorShift.G.Max);
            var blueAtmosphereShift = random.NextFloat(generatorDef.HostileAtmosphereColorShift.B.Min, generatorDef.HostileAtmosphereColorShift.B.Max);

            var atmosphereWavelengths = new Vector3(0.650f + redAtmosphereShift, 0.570f + greenAtmosphereShift, 0.475f + blueAtmosphereShift);

            atmosphereWavelengths.X = MathHelper.Clamp(atmosphereWavelengths.X, 0.1f, 1.0f);
            atmosphereWavelengths.Y = MathHelper.Clamp(atmosphereWavelengths.Y, 0.1f, 1.0f);
            atmosphereWavelengths.Z = MathHelper.Clamp(atmosphereWavelengths.Z, 0.1f, 1.0f);

            var entityId = GetPlanetEntityId($"proc_planet_{provider.Seed}_{(int) provider.Radius}_{(long) pos.X}_{(long) pos.Y}_{(long) pos.Z}");
            var result = MyAPIGateway.Entities.GetEntityById(entityId);
            if (result != null)
                return result as MyPlanet;
            var planet = new MyPlanet();
            planet.EntityId = entityId;
            MyPlanetInitArguments planetInitArguments = new MyPlanetInitArguments();
            planetInitArguments.StorageName = storageName;
            CastProhibit(storage, out planetInitArguments.Storage);
            var posMinCorner = pos - provider.Radius;
            planetInitArguments.PositionMinCorner = posMinCorner;
            planetInitArguments.Radius = (float) provider.Radius;
            planetInitArguments.AtmosphereRadius = atmosphereRadius;
            planetInitArguments.MaxRadius = (float) outerRadius;
            planetInitArguments.MinRadius = (float) innerRadius;
            planetInitArguments.HasAtmosphere = generatorDef.HasAtmosphere;
            planetInitArguments.AtmosphereWavelengths = atmosphereWavelengths;
            planetInitArguments.GravityFalloff = generatorDef.GravityFalloffPower;
            planetInitArguments.MarkAreaEmpty = true;
            MoveAtmosphereSettings(generatorDef.AtmosphereSettings, out planetInitArguments.AtmosphereSettings);
            planetInitArguments.SurfaceGravity = generatorDef.SurfaceGravity;
            planetInitArguments.AddGps = false;
            planetInitArguments.SpherizeWithDistance = true;
            planetInitArguments.Generator = generatorDef;
            planetInitArguments.UserCreated = false;
            planetInitArguments.InitializeComponents = true;

            planet.Init(planetInitArguments);

            MyEntities.Add(planet);
            MyEntities.RaiseEntityCreated(planet);

            return planet;
        }

        public static bool TryFindAsteroidSeed(ref int seed, float size, HashSet<string> prohibitsOre, HashSet<string> requiresOre, int maxTries = 5)
        {
            if (requiresOre.Count == 0 && prohibitsOre.Count == 0)
                return true;
            var gen = AsteroidShapeGenerator.AsteroidGenerators[MyAPIGateway.Session.SessionSettings.VoxelGeneratorVersion];

            for (var i = 0; i < maxTries; i++)
            {
                AsteroidShapeGenerator.CompositeShapeGeneratedDataBuilder data;
                gen(seed, size, out data);
                if (requiresOre.Count == 0 || requiresOre.Contains(data.DefaultMaterial.MinedOre) || ContainsAny(data.Deposits, requiresOre))
                    if (prohibitsOre.Count == 0 || (!prohibitsOre.Contains(data.DefaultMaterial.MinedOre) && !ContainsAny(data.Deposits, prohibitsOre)))
                    {
                        return true;
                    }

                seed *= 982451653;
            }

            return false;
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private static bool ContainsAny(AsteroidShapeGenerator.CompositeShapeOreDepositBuilder[] builders, HashSet<string> test)
        {
            foreach (var k in builders)
                if (test.Contains(k.Material.MinedOre))
                    return true;
            return false;
        }
    }
}