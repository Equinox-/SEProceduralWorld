using Equinox.Utils.DotNet;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public class MyCompositeShapeProviderBuilder : IMyStorageDataProviderBuilder
    {
        public uint Version;
        public int Generator;
        public int Seed;
        public float Size;
        public uint UnusedCompat;

        // From GitHub
        public int ProviderTypeId => 10002;

        public int SerializedSize => (4 * 5);

        public void WriteTo(MyMemoryStream stream)
        {
            stream.Write(Version);
            stream.Write(Generator);
            stream.Write(Seed);
            stream.Write(Size);
            stream.Write(UnusedCompat);
        }


        // From GitHub
        public const int AsteroidGeneratorCount = 3;
        private const uint CURRENT_VERSION = 2;

        public static MyCompositeShapeProviderBuilder CreateAsteroidShape(int seed, float size, int generatorEntry)
        {
            if (generatorEntry > AsteroidGeneratorCount - 1)
                generatorEntry = AsteroidGeneratorCount - 1;
            else if (generatorEntry < 0)
                generatorEntry = 0;
            return new MyCompositeShapeProviderBuilder
            {
                Version = CURRENT_VERSION,
                Generator = generatorEntry,
                Seed = seed,
                Size = size,
                UnusedCompat = 0
            };
        }
    }
}
