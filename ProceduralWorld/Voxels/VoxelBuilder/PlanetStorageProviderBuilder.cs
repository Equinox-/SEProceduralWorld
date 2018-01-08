using System;
using System.Text;
using Equinox.Utils.Stream;
using Sandbox.Definitions;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public class PlanetStorageProviderBuilder : IStorageDataProviderBuilder
    {
        // From GitHub
        public int ProviderTypeId => 10042;

        public long Version;
        public long Seed;
        public double Radius;
        public MyPlanetGeneratorDefinition Generator;

        public Vector3I StorageSize { get; private set; }

        public int SerializedSize
        {
            get
            {
                var size = Encoding.UTF8.GetByteCount(Generator.Id.SubtypeName); // string length
                size += (MathHelper.Log2Floor(size) + 6) / 7; // 7-bit encoded string size.

                return (8+8+8) + size;
            }
        }

        public void WriteTo(MemoryStream stream)
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
            Init();
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
            Init();
        }

        private void Init()
        {
            var rad = (float)Radius;

            var maxHeight = rad * Generator.HillParams.Max;

            var halfSize = rad + maxHeight;
            StorageSize = MyVoxelCoordSystems.FindBestOctreeSize(2 * halfSize);
        }
    }
}
