using System;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.VRage;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class MyProceduralWorld
    {
        private static MyProceduralWorld m_instance;

        public static MyProceduralWorld Instance => m_instance ?? (m_instance = new MyProceduralWorld());

        public readonly ulong Seed;

        private readonly IMyModule m_factionNoise;
        private readonly IMyModule m_oreNoise;
        public readonly MyOctreeNoise StationNoise;

        private MyProceduralWorld()
        {
            Seed = MyAPIGateway.Session.WorkshopId ?? (ulong)MyAPIGateway.Session.SessionSettings.ProceduralSeed;
            var rand = new Random((int)((Seed >> 32) ^ Seed));
            m_factionNoise = new MySimplex(rand.Next(), 1.0 / Settings.Instance.FactionDensity);
            m_oreNoise = new MyCompositeNoise(8, 1 / (float) Settings.Instance.OreMapDensity, rand.Next());
            StationNoise = new MyOctreeNoise(rand.NextLong(), Settings.Instance.StationMaxSpacing, Settings.Instance.StationMinSpacing, null);
        }
        
        public MyProceduralFactionSeed SeedAt(Vector3D pos)
        {
            long noise = 0;
            for (var i = 0; i < 60 / Settings.Instance.FactionShiftBase; i++)
            {
                var ln = (long) (m_factionNoise.GetValue(pos) * (1 << Settings.Instance.FactionShiftBase));
                if (ln < 0) ln = 0;
                if (ln >= (1 << Settings.Instance.FactionShiftBase)) ln = (1 << Settings.Instance.FactionShiftBase) - 1;
                noise |= ln << (i * Settings.Instance.FactionShiftBase);
                pos /= 2.035;
            }
            return new MyProceduralFactionSeed(noise);
        }

        public float OreConcentrationAt(MyDefinitionId oreID, Vector3D localPos)
        {
            var hashCode = oreID.SubtypeName.GetHashCode();
            localPos.X += (hashCode & 0xFF) * 104.58F;
            localPos.Y += ((hashCode >> 8) & 0xFF) * 92.75F;
            localPos.Z += ((hashCode >> 16) & 0xFF) * 119.85F;
            var res = (float)m_oreNoise.GetValue(localPos);
            // *guess* rarity from the recipe's output:input ratio.
            var avgOutputRatio = MyBlueprintIndex.Instance.GetAllConsuming(oreID).Select(x => x.Ingredients.Values.Sum(y => (double)y)).DefaultIfEmpty(1).Average();
            res *= (float)Math.Sqrt(avgOutputRatio);
            return MyMath.Clamp(res, 0, 1);
        }
    }
}
