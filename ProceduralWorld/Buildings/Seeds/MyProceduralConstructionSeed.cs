using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Utils;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class MyProceduralConstructionSeed
    {
        public readonly long Seed;
        public readonly Vector3D Location;
        public readonly Quaternion Orientation;
        public MatrixD WorldMatrix
        {
            get
            {
                var m = MatrixD.CreateFromQuaternion(Orientation);
                m.Translation = Location;
                return m;
            }
        }
        public readonly MyProceduralFactionSeed Faction;

        private readonly Dictionary<MyDefinitionId, MyTradeRequirements> m_exports;
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> Exports => m_exports;

        private readonly Dictionary<MyDefinitionId, MyTradeRequirements> m_imports;
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> Imports => m_imports;

        private readonly Dictionary<MyDefinitionId, MyTradeRequirements> m_localStorage;
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> LocalStorage => m_localStorage;

        /// <summary>
        /// Exports, imports, and local storage.
        /// </summary>
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> AllStorage => m_exports.Concat(m_imports).Concat(m_localStorage);
        /// <summary>
        /// Exports and local storage.
        /// </summary>
        public IEnumerable<KeyValuePair<MyDefinitionId, MyTradeRequirements>> ProductionStorage => m_exports.Concat(m_localStorage);

        public readonly MyDefinitionId? SpecialityExport;
        public readonly MyProceduralStationSpeciality Speciality;
        public readonly string Name;

        public MyProceduralConstructionSeed(Vector4D locationDepth, Quaternion? orientation, long seed)
        {
            Seed = seed;
            Location = new Vector3D(locationDepth.X, locationDepth.Y, locationDepth.Z);
            Faction = MyProceduralWorld.Instance.SeedAt(Location);
            var m_tmpRandom = new Random((int)seed);
            var density = locationDepth.W;
            Population = (int)MyMath.Clamp((float)(Math.Round(5 * m_tmpRandom.NextExponential() * density)), 1, 100);
            var sqrtPopulation = Math.Sqrt(Population);

            var choice = new MyWeightedChoice<MyProceduralStationSpeciality>();
            foreach (var kv in Faction.Specialities)
                foreach (var kt in kv.Key.StationSpecialities)
                    choice.Add(kt.Key, kv.Value * kt.Value);
            Speciality = choice.Choose(m_tmpRandom.NextDouble(), MyWeightedChoice<MyProceduralStationSpeciality>.WeightedNormalization.ClampToZero);
            m_exports = new Dictionary<MyDefinitionId, MyTradeRequirements>(MyDefinitionId.Comparer);
            m_imports = new Dictionary<MyDefinitionId, MyTradeRequirements>(MyDefinitionId.Comparer);
            List<MyDefinitionBase> rchoice = null;
            var specialExport = m_tmpRandom.NextDouble() <= Speciality.SpecializationChance;
            if (specialExport)
                rchoice = new List<MyDefinitionBase>();
            foreach (var kv in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (Speciality.CanExport(kv))
                {
                    m_exports.Add(kv.Id, default(MyTradeRequirements));
                    rchoice?.Add(kv);
                }
                if (Speciality.CanImport(kv))
                    m_imports.Add(kv.Id, default(MyTradeRequirements));
            }
            SpecialityExport = null;
            if (specialExport)
            {
                // ReSharper disable once PossibleNullReferenceException
                SpecialityExport = rchoice[m_tmpRandom.Next(0, rchoice.Count)].Id;
                m_exports.Clear();
                m_exports.Add(SpecialityExport.Value, default(MyTradeRequirements));
            }

            var nameBuilder = new StringBuilder();
            nameBuilder.Append(Faction.FounderName).Append("'s ");
            if (SpecialityExport.HasValue)
                nameBuilder.Append(MyDefinitionManager.Static.GetDefinition(SpecialityExport.Value).DisplayNameText).Append(" ");
            else if (Speciality.GeneralizedPrefixes != null && Speciality.GeneralizedPrefixes.Length > 0)
                nameBuilder.Append(m_tmpRandom.NextUniformChoice(Speciality.GeneralizedPrefixes)).Append(" ");
            nameBuilder.Append(m_tmpRandom.NextUniformChoice(Speciality.Suffixes));
            Name = nameBuilder.ToString();

            // Compute hotlisted import amounts
            {
                var keys = m_imports.Keys.ToList();
                foreach (var kv in keys)
                {
                    var baseMult = Population * m_tmpRandom.NextExponential() / keys.Count;
                    var k = MyDefinitionManager.Static.GetDefinition(kv);
                    var pi = k as MyPhysicalItemDefinition;
                    if (k is MyComponentDefinition)
                        baseMult *= 100 / pi.Mass;
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ore))
                        baseMult *= 10000 / (1 + Math.Sqrt(MyOreUtilities.GetRarity(k.Id)));
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ingot))
                        baseMult *= 5000 * MyOreUtilities.GetOutputRatio(k.Id);
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_GasProperties))
                        baseMult *= 250000; // 1/10th a large tank
                    else if (pi != null)
                        baseMult *= 10 / pi.Mass;

                    m_imports[kv] = new MyTradeRequirements(baseMult, Math.Sqrt(baseMult));
                }
            }

            // Compute exported amounts
            {
                var keys = m_exports.Keys.ToList();
                foreach (var kv in keys)
                {
                    var baseMult = Population * Population * m_tmpRandom.NextExponential() / keys.Count;
                    var k = MyDefinitionManager.Static.GetDefinition(kv);
                    var pi = k as MyPhysicalItemDefinition;
                    if (k is MyComponentDefinition)
                        baseMult *= 100 / pi.Mass;
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ore))
                        baseMult *= 10000 / (1 + Math.Sqrt(MyOreUtilities.GetRarity(k.Id)));
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ingot))
                        baseMult *= 5000 * MyOreUtilities.GetOutputRatio(k.Id);
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_GasProperties))
                        baseMult *= 250000; // 1/10th a large tank
                    m_exports[kv] = new MyTradeRequirements(baseMult, Math.Sqrt(baseMult));
                }
            }

            // Using exports, compute imports
            {
                foreach (var kv in m_exports)
                {
                    var producer = MyBlueprintIndex.Instance.GetAllProducing(kv.Key, true).ToList();
                    foreach (var blueprint in producer)
                        foreach (var ingredient in blueprint.Ingredients)
                        {
                            var aStorage = kv.Value.Storage * (double)ingredient.Value / producer.Count;
                            var aThroughput = kv.Value.Throughput * (double)ingredient.Value / producer.Count;
                            MyTradeRequirements current;
                            m_imports[ingredient.Key] = m_imports.TryGetValue(ingredient.Key, out current) ?
                                new MyTradeRequirements(current.Storage + aStorage, current.Throughput + aThroughput) :
                                new MyTradeRequirements(aStorage, aThroughput);
                        }
                }
            }

            m_localStorage = new Dictionary<MyDefinitionId, MyTradeRequirements>(MyDefinitionId.Comparer)
            {
                // ~3MWH / person stored, ~1MW / person produced
                [MyResourceDistributorComponent.ElectricityId] = new MyTradeRequirements(Population * 3 * m_tmpRandom.NextNormal(1, 0.1), Population * m_tmpRandom.NextNormal(1, 0.1), 10, 10),
                // one large O2 tank / person stored (~18 days), 1 O2/sec/person produced
                [MyResourceDistributorComponent.OxygenId] = new MyTradeRequirements(Population * 1e5 * m_tmpRandom.NextNormal(1, 0.1), Population * 1 * m_tmpRandom.NextNormal(1, 0.1), 10, 10),
                // I don't even know how I'd guess this
                [MyResourceDistributorComponent.HydrogenId] = new MyTradeRequirements(Population * 1e4 * m_tmpRandom.NextExponential(), m_tmpRandom.NextExponential() * sqrtPopulation)
            };

            // Compute mass & volume of storage
            var vol = Vector2D.Zero;
            foreach (var kv in AllStorage)
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
            m_blockCountRequirements = new Dictionary<MySupportedBlockTypes, MyBlockRequirement>(MySupportedBlockTypesEquality.Instance);
            // living quarters.
            var addlLiving = Faction.AttributeWeight(MyProceduralFactionSpeciality.Housing);
            m_blockCountRequirements[MySupportedBlockTypes.CryoChamber] = Population + Math.Max(0, (int)Math.Round(sqrtPopulation * (m_tmpRandom.NextNormal(1, 2) + addlLiving)));
            m_blockCountRequirements[MySupportedBlockTypes.MedicalRoom] = new MyBlockRequirement(Math.Max(1, (int)Math.Round(sqrtPopulation * (m_tmpRandom.NextNormal(0.5, 0.5) + Math.Max(addlLiving, Faction.AttributeWeight(MyProceduralFactionSpeciality.Military))))), 1e3);
            m_blockCountRequirements[MySupportedBlockTypes.ShipController] = Math.Max(1, (int)Math.Round(Population * m_tmpRandom.NextNormal(0.5, 0.5)));
            // how "defensive" this group is
            m_blockCountRequirements[MySupportedBlockTypes.Weapon] = Math.Max(0, (int)Math.Round(Population * m_tmpRandom.NextNormal(2, 2) * Faction.AttributeWeight(MyProceduralFactionSpeciality.Military)));
            // ship repair?
            m_blockCountRequirements[MySupportedBlockTypes.ShipConstruction] = Math.Max(0, (int)Math.Round(Population * m_tmpRandom.NextNormal(4, 3) * Faction.AttributeWeight(MyProceduralFactionSpeciality.Repair)));
            // docking?
            m_blockCountRequirements[MySupportedBlockTypes.Docking] = Math.Max(1, (int)Math.Round(sqrtPopulation * m_tmpRandom.NextNormal(1, 1) * Faction.AttributeWeight(MyProceduralFactionSpeciality.Housing)));
            // comms?
            m_blockCountRequirements[MySupportedBlockTypes.Communications] = new MyBlockRequirement(Math.Max(1, (int)Math.Round(sqrtPopulation * MyMath.Clamp((float)m_tmpRandom.NextNormal(), 0, 1))), 1e6);

            Orientation = m_tmpRandom.NextQuaternion();
            // Branched, since we want to consume a quaternion from the random.
            if (orientation != null)
                Orientation = orientation.Value;
        }

        // Uses (v) to compute some noise in the [0-1] range.
        public double DeterministicNoise(int v)
        {
            const int prime1 = 1299721;
            const int prime2 = 8803;
            const int prime3 = 179426453;
            var cti = (uint)v;
            cti ^= (uint)Seed;
            cti *= prime3;
            cti ^= prime1;
            cti += prime2;
            cti *= prime1;
            cti ^= prime3;
            cti *= prime3;
            cti ^= (uint)(Seed >> 32);
            return (double)cti / uint.MaxValue;
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

            public readonly double ThroughputErrorMultiplier, StorageErrorMultiplier;

            public MyTradeRequirements(double storage, double throughput, double storageErrMult = 1, double throughErrMult = 1)
            {
                Storage = Math.Round(storage * 1e4) / 1e4;
                Throughput = Math.Round(throughput * 1e4) / 1e4;
                StorageErrorMultiplier = Math.Round(storageErrMult * 1e2) / 1e2;
                ThroughputErrorMultiplier = Math.Round(throughErrMult * 1e2) / 1e2;
            }

            public override string ToString()
            {
                var builder = new StringBuilder(256);
                builder.Append("TradeRequirements[Storage=")
                    .Append(Storage)
                    .Append(", Throughput=")
                    .Append(Throughput)
                    .Append(", StorageErrMult=")
                    .Append(StorageErrorMultiplier)
                    .Append(", ThroughputErrMult=")
                    .Append(ThroughputErrorMultiplier)
                    .Append("]");
                return builder.ToString();
            }
        }

        public struct MyBlockRequirement
        {
            public readonly int Count;
            public readonly double Multiplier;

            public static readonly MyBlockRequirement Zero = new MyBlockRequirement(0);

            public MyBlockRequirement(int c, double mult = 1)
            {
                Count = c;
                Multiplier = mult;
            }

            public static implicit operator MyBlockRequirement(int value)
            {
                return new MyBlockRequirement(value);
            }

            public override string ToString()
            {
                var builder = new StringBuilder(256);
                builder.Append("BlockRequirement[Count=")
                    .Append(Count)
                    .Append(", Mult=")
                    .Append(Multiplier)
                    .Append("]");
                return builder.ToString();
            }
        }

        private readonly Dictionary<MySupportedBlockTypes, MyBlockRequirement> m_blockCountRequirements;

        public IEnumerable<KeyValuePair<MySupportedBlockTypes, MyBlockRequirement>> BlockCountRequirements => m_blockCountRequirements;

        public MyBlockRequirement BlockCountRequirement(MySupportedBlockTypes key)
        {
            return m_blockCountRequirements.GetValueOrDefault(key, MyBlockRequirement.Zero);
        }

        public override string ToString()
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("MyProceduralConstructionSeed[");
            builder.Append("\tName=\"").Append(Name).AppendLine("\"");
            builder.Append("\tSeed=").AppendLine(Seed.ToString());
            builder.Append("\tLocation=").AppendLine(Location.ToString());
            builder.Append("\tOrientation=").AppendLine(Orientation.ToString());
            builder.Append("\tSpeciality=").AppendLine(Speciality.ToString());
            builder.Append("\tPopulation=").Append(Population).AppendLine();
            builder.Append("\tSpecialityExport=");
            builder.AppendLine(SpecialityExport?.ToString() ?? "None");
            builder.Append("\tFaction=").AppendLine(Faction.ToString().Replace("\n", "\n\t"));
            builder.AppendLine("\tExports=[");
            foreach (var kv in m_exports)
                builder.Append("\t\t").Append(kv.Key).Append("=").AppendLine(kv.Value.ToString());
            builder.AppendLine("\t]");
            builder.AppendLine("\tImports=[");
            foreach (var kv in m_imports)
                builder.Append("\t\t").Append(kv.Key).Append("=").AppendLine(kv.Value.ToString());
            builder.AppendLine("\t]");
            builder.AppendLine("\tLocal Storage=[");
            foreach (var kv in m_localStorage)
                builder.Append("\t\t").Append(kv.Key).Append("=").AppendLine(kv.Value.ToString());
            builder.AppendLine("\t]");
            builder.AppendLine("\tBlock Counts=[");
            foreach (var kv in m_blockCountRequirements)
                builder.Append("\t\t").Append(kv.Key).Append("=").AppendLine(kv.Value.ToString());
            builder.AppendLine("\t]");
            builder.AppendLine("]");
            return builder.ToString();
        }
    }
}