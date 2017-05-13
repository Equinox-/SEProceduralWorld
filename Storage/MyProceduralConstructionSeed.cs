using System;
using System.Collections.Generic;
using System.Linq;
using ProcBuild.Utils;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Storage
{
    public class MyProceduralConstructionSeed
    {
        public readonly long Seed;
        private readonly Random m_random;
        public readonly MyProceduralEnvironment Environment;

        public MyProceduralConstructionSeed(MyProceduralEnvironment env, long seed)
        {
            Seed = seed;
            Environment = env;
            m_random = new Random((int)seed);

            HueRotation = (float)m_random.NextDouble();
            SaturationModifier = MyMath.Clamp((float)m_random.NextNormal(), -1, 1);
            ValueModifier = MyMath.Clamp((float)m_random.NextNormal(), -1, 1);

            Population = (int)MyMath.Clamp((float)Math.Round(5 * m_random.NextExponential()), 1, 100);
            var sqrtPopulation = Math.Sqrt(Population);

            m_tradeRequirements = new Dictionary<MyDefinitionId, MyTradeRequirements>();
            foreach (var x in MyDefinitionManager.Static.GetPhysicalItemDefinitions().Select(x => x.Id).Where(x => x.TypeId == typeof(MyObjectBuilder_Ore)))
            {
                var concentration = Environment.OreConcentrationHere(x);
                if (concentration <= float.Epsilon) continue;
                // Lots of ores.
                // Store ~10e6 at most.  Refine about 1e3/sec at most.
                m_tradeRequirements[x] = new MyTradeRequirements(
                    concentration * Population * 10e6 * m_random.NextExponential(), concentration * sqrtPopulation * 1e2 * m_random.NextExponential());
            }

            foreach (var x in MyDefinitionManager.Static.GetPhysicalItemDefinitions().Select(x => x.Id).Where(x => x.TypeId == typeof(MyObjectBuilder_Ingot))
                .Concat(MyDefinitionManager.Static.GetDefinitionsOfType<MyComponentDefinition>().Select(x => x.Id)))
            {
                var concentration = Math.Max(0, m_random.NextNormal());
                var bpItem = MyBlueprintIndex.Instance.GetTopLevel(x);
                if (bpItem != null)
                {
                    var minThroughput = double.MaxValue;
                    foreach (var kv in bpItem.Ingredients)
                    {
                        MyTradeRequirements req;
                        if (!m_tradeRequirements.TryGetValue(kv.Key, out req)) continue;
                        minThroughput = Math.Min(minThroughput, req.Throughput / (double)kv.Value);
                    }
                    if (minThroughput < double.MaxValue)
                        concentration = minThroughput / Math.Sqrt(Population);
                }
                if (concentration <= float.Epsilon) continue;
                m_tradeRequirements[x] = new MyTradeRequirements(concentration * Population * m_random.NextExponential(), concentration * sqrtPopulation * m_random.NextExponential());
            }

            foreach (var gas in MyDefinitionManager.Static.GetDefinitionsOfType<MyGasProperties>())
                m_tradeRequirements[gas.Id] = new MyTradeRequirements(Population * m_random.NextExponential(), m_random.NextExponential());

            // Add in known stuff (forced)
            // ~3MWH / person stored, ~1MW / person produced
            m_tradeRequirements[MyResourceDistributorComponent.ElectricityId] =
                new MyTradeRequirements(Population * 3 * m_random.NextNormal(1, 0.1), Population * m_random.NextNormal(1, 0.1));
            // one large O2 tank / person stored (~18 days), 1 O2/sec/person produced
            m_tradeRequirements[MyResourceDistributorComponent.OxygenId] =
                new MyTradeRequirements(Population * 1e5 * m_random.NextNormal(1, 0.1), Population * 1 * m_random.NextNormal(1, 0.1));
            // I don't even know how I'd guess this
            m_tradeRequirements[MyResourceDistributorComponent.HydrogenId] =
                new MyTradeRequirements(Population * m_random.NextExponential(), m_random.NextExponential() * sqrtPopulation);

            // Compute mass & volume of storage
            var vol = Vector2D.Zero;
            foreach (var kv in m_tradeRequirements)
            {
                MyDefinitionBase ob;
                if (!MyDefinitionManager.Static.TryGetDefinition(kv.Key, out ob))
                    continue;
                var physItem = ob as MyPhysicalItemDefinition;
                if (physItem != null)
                    vol += kv.Value.Storage * new Vector2D(physItem.Volume, physItem.Mass);
                var component = ob as MyComponentDefinition;
                if (component != null)
                    vol += kv.Value.Storage * new Vector2D(component.Volume, component.Mass);
                var ammo = ob as MyAmmoMagazineDefinition;
                if (ammo != null)
                    vol += kv.Value.Storage * new Vector2D(ammo.Volume, ammo.Mass);
            }
            StorageVolume = vol.X;
            StorageMass = vol.Y;
        }

        // 0 to 1
        public readonly float HueRotation;
        // -1 to 1
        public readonly float SaturationModifier;
        // -1 to 1
        public readonly float ValueModifier;

        public readonly int Population;

        public readonly double StorageVolume;
        public readonly double StorageMass;

        public struct MyTradeRequirements
        {
            /// <summary>
            /// Hint used to determine the number of this item we want.
            /// </summary>
            public readonly double Storage;
            /// <summary>
            /// Hint used to choose how many factories we need.
            /// </summary>
            public readonly double Throughput;

            public MyTradeRequirements(double storage, double throughput)
            {
                Storage = storage;
                Throughput = throughput;
            }
        }

        private readonly Dictionary<MyDefinitionId, MyTradeRequirements> m_tradeRequirements;
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> TradeRequirements => m_tradeRequirements;
    }
}