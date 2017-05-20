using System;
using System.Collections.Generic;
using System.Linq;
using ProcBuild.Seeds;
using ProcBuild.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Storage
{
    public class MyProceduralConstructionSeed
    {
        public readonly long Seed;
        public readonly Random Random;
        public readonly Vector3D Location;
        public readonly MyProceduralFactionSeed Faction;

        public MyProceduralConstructionSeed(Vector3D location, MyProceduralFactionSeed faction, long seed)
        {
            Seed = seed;
            Faction = faction;
            Location = location;
            Random = new Random((int)seed);

            Population = (int)MyMath.Clamp((float)Math.Round(5 * Random.NextExponential()), 1, 100);
            var sqrtPopulation = Math.Sqrt(Population);

            m_tradeRequirements = new Dictionary<MyDefinitionId, MyTradeRequirements>();
            foreach (var x in MyDefinitionManager.Static.GetPhysicalItemDefinitions().Select(x => x.Id).Where(x => x.TypeId == typeof(MyObjectBuilder_Ore)))
            {
                var concentration = MyProceduralWorld.Instance.OreConcentrationAt(x, location);
                if (concentration <= float.Epsilon) continue;
                // Lots of ores.
                // Store ~10e6 at most.  Refine about 1e3/sec at most.
                m_tradeRequirements[x] = new MyTradeRequirements(
                    concentration * Population * 10e6 * Random.NextExponential(), concentration * sqrtPopulation * 10 * Random.NextExponential());
            }

            foreach (var x in MyDefinitionManager.Static.GetPhysicalItemDefinitions().Select(x => x.Id).Where(x => x.TypeId == typeof(MyObjectBuilder_Ingot))
                .Concat(MyDefinitionManager.Static.GetDefinitionsOfType<MyComponentDefinition>().Select(x => x.Id)))
            {
                var throughput = Math.Max(0, Random.NextNormal()) * sqrtPopulation * Random.NextExponential();
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
                        throughput = minThroughput;
                }
                if (throughput <= float.Epsilon) continue;
                m_tradeRequirements[x] = new MyTradeRequirements(throughput * 60 * 60 * 4 * Random.NextExponential(), throughput);
            }

            foreach (var gas in MyDefinitionManager.Static.GetDefinitionsOfType<MyGasProperties>())
                m_tradeRequirements[gas.Id] = new MyTradeRequirements(Population * Random.NextExponential(), Random.NextExponential());

            // Add in known stuff (forced)
            // ~3MWH / person stored, ~1MW / person produced
            m_tradeRequirements[MyResourceDistributorComponent.ElectricityId] =
                new MyTradeRequirements(Population * 3 * Random.NextNormal(1, 0.1), Population * Random.NextNormal(1, 0.1));
            // one large O2 tank / person stored (~18 days), 1 O2/sec/person produced
            m_tradeRequirements[MyResourceDistributorComponent.OxygenId] =
                new MyTradeRequirements(Population * 1e5 * Random.NextNormal(1, 0.1), Population * 1 * Random.NextNormal(1, 0.1));
            // I don't even know how I'd guess this
            m_tradeRequirements[MyResourceDistributorComponent.HydrogenId] =
                new MyTradeRequirements(Population * Random.NextExponential(), Random.NextExponential() * sqrtPopulation);

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

            // ReSharper disable once UseObjectOrCollectionInitializer
            m_blockCountRequirements = new Dictionary<MySupportedBlockTypes, int>();
            // living quarters.
            m_blockCountRequirements[MySupportedBlockTypes.CryoChamber] = Population + Math.Max(0, (int)Math.Round(sqrtPopulation * Random.NextNormal(1, 2)));
            m_blockCountRequirements[MySupportedBlockTypes.MedicalRoom] = Math.Max(1, (int)Math.Round(sqrtPopulation * Random.NextNormal(0.5, 0.5)));
            m_blockCountRequirements[MySupportedBlockTypes.ShipController] = Math.Max(1, (int)Math.Round(Population * Random.NextNormal(0.5, 0.5)));
            // how "defensive" this group is
            m_blockCountRequirements[MySupportedBlockTypes.Weapon] = Math.Max(0, (int)Math.Round(Population * Random.NextNormal(2, 2) * faction.Militaristic));
            // ship repair?
            m_blockCountRequirements[MySupportedBlockTypes.ShipConstruction] = Math.Max(0, (int)Math.Round(Population * Random.NextNormal(4, 3) * faction.Services));
            // docking?
            m_blockCountRequirements[MySupportedBlockTypes.Docking] = Math.Max(1, (int)Math.Round(sqrtPopulation * Random.NextNormal(1, 1) * faction.Commercialistic));
            // comms?
            m_blockCountRequirements[MySupportedBlockTypes.Communications] = Math.Max(1, (int)Math.Round(sqrtPopulation * MyMath.Clamp((float)Random.NextNormal(), 0, 1) * faction.Commercialistic));
        }


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

        private readonly Dictionary<MySupportedBlockTypes, int> m_blockCountRequirements;
        public IEnumerable<KeyValuePair<MySupportedBlockTypes, int>> BlockCountRequirements => m_blockCountRequirements;

        public int BlockCountRequirement(MySupportedBlockTypes key)
        {
            return m_blockCountRequirements.GetValueOrDefault(key, 0);
        }
    }
}