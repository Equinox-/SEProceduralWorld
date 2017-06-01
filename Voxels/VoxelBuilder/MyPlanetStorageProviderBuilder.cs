using System;
using System.Text;
using Equinox.Utils.DotNet;
using Sandbox.Definitions;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public class MyPlanetStorageProviderBuilder : IMyStorageDataProviderBuilder
    {
        // From GitHub
        public int ProviderTypeId => 10042;

        public long Version;
        public long Seed;
        public double Radius;
        public MyPlanetGeneratorDefinition Generator;

        public int SerializedSize
        {
            get
            {
                var size = Encoding.UTF8.GetByteCount(Generator.Id.SubtypeName); // string length
                size += (MathHelper.Log2Floor(size) + 6) / 7; // 7-bit encoded string size.

                return (8+8+8) + size;
            }
        }

        public void WriteTo(MyMemoryStream stream)
        {
            stream.Write(Version);
            stream.Write(Seed);
            stream.Write(Radius);
            stream.Write(Generator.Id.SubtypeName);
        }

        // From GitHub
        private static readonly int STORAGE_VERSION = 1;

        public void Init(long seed, MyPlanetGeneratorDefinition generator, double radius)
        {
            radius = Math.Max(radius, 1.0);
            Generator = generator;
            Radius = radius;
            Seed = seed;
            Version = STORAGE_VERSION;
        }

        public void Init(long seed, string generator, double radius)
        {
            radius = Math.Max(radius, 1.0);
            var def = MyDefinitionManager.Static.GetDefinition<MyPlanetGeneratorDefinition>(MyStringHash.GetOrCompute(generator));
            if (def == null) throw new Exception($"Cannot load planet generator definition for subtype '{generator}'.");
            Generator = def;
            Radius = radius;
            Seed = seed;
            Version = STORAGE_VERSION;
        }
    }
}
