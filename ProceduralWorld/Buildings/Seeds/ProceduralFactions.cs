using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Names;
using Equinox.Utils;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.Keen;
using Equinox.Utils.Random;
using Equinox.Utils.Session;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class ProceduralFactions : LoggingSessionComponent
    {
        private IMyModule m_factionNoise;
        private double m_factionDensity = 5e5;
        private int m_factionShiftBase = 1;
        private long m_seed = 1;

        private void RebuildNoiseModule()
        {
            m_factionNoise = new MySimplex((int)m_seed, 1.0 / m_factionDensity);
        }

        private NameGeneratorBase m_names;
        private BuildingDatabase m_database;
        public ProceduralFactions()
        {
            RebuildNoiseModule();
            DependsOn<NameGeneratorBase>(x => { m_names = x; });
            DependsOn<BuildingDatabase>(x => { m_database = x; });
        }

        public static readonly Type[] SuppliedDeps = { typeof(ProceduralFactions) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        public ProceduralFactionSeed SeedAt(Vector3D pos)
        {
            ulong noise = 0;
            for (var i = 0; i < 60 / m_factionShiftBase; i++)
            {
                var noiseSegment = (long)(m_factionNoise.GetValue(pos) * (1L << m_factionShiftBase));
                if (noiseSegment < 0) noiseSegment = 0;
                if (noiseSegment >= (1L << m_factionShiftBase))
                    noiseSegment = (1L << m_factionShiftBase) - 1;
                noise |= (ulong)noiseSegment << (i * m_factionShiftBase);
                pos /= 2.035;
            }
            Ob_ProceduralFaction recipe;
            if (m_database.TryGetFaction(noise, out recipe))
                return new ProceduralFactionSeed(recipe);
            var rand = new Random((int) noise);
            var nameSeed = (ulong) rand.NextLong();
            var stationSeed = (ulong) rand.NextLong();
            var result = new ProceduralFactionSeed(m_names.Generate(nameSeed), stationSeed);
            m_database.StoreFactionBlueprint(result);
            return result;
        }

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_ProceduralFactions;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
            m_factionShiftBase = config.FactionShiftBase;
            m_factionDensity = config.FactionDensity;
            m_seed = config.Seed;
            RebuildNoiseModule();
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_ProceduralFactions() { Seed = m_seed, FactionDensity = m_factionDensity, FactionShiftBase = m_factionShiftBase };
        }
    }

    public class Ob_ProceduralFactions : Ob_ModSessionComponent
    {
        public long Seed = 12378123;
        // (500 km)^3 cells.
        // For context, 250e3 for Earth-Moon, 2300e3 for Earth-Mars, 6000e3 for Earth-Alien
        public double FactionDensity = 5e5;
        // There will be roughly (1<<FactionShiftBase) factions per cell.
        public int FactionShiftBase = 1;
    }
}
