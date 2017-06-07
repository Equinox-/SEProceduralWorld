using System;
using System.Collections.Generic;
using System.Diagnostics;
using Equinox.Utils.DotNet;
using Equinox.Utils.Noise.VRage;
using Sandbox.Definitions;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.VoxelBuilder
{
    public class MyCompositeShapeProviderBuilder : IMyStorageDataProviderBuilder
    {
        public uint Version;
        public int Generator;
        public int Seed;
        public float Size;
        public uint UnusedCompat;
        public MyAsteroidShapeGenerator.MyCompositeShapeGeneratedDataBuilder GeneratedData;

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

            var gen = MyAsteroidShapeGenerator.AsteroidGenerators[generatorEntry];
            var result = new MyCompositeShapeProviderBuilder
            {
                Version = CURRENT_VERSION,
                Generator = generatorEntry,
                Seed = seed,
                Size = size,
                UnusedCompat = 0
            };
            gen(seed, size, out result.GeneratedData);
            return result;
        }
    }
}
