using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils;
using Equinox.Utils.Definitions;
using Equinox.Utils.Random;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class ProceduralConstructionSeed
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
        public readonly ProceduralFactionSeed Faction;

        private readonly Dictionary<MyDefinitionId, TradeRequirements> m_exports;
        public IEnumerable<KeyValuePair<MyDefinitionId, TradeRequirements>> Exports => m_exports;

        private readonly Dictionary<MyDefinitionId, TradeRequirements> m_imports;
        public IEnumerable<KeyValuePair<MyDefinitionId, TradeRequirements>> Imports => m_imports;

        private readonly Dictionary<MyDefinitionId, TradeRequirements> m_localStorage;
        public IEnumerable<KeyValuePair<MyDefinitionId, TradeRequirements>> LocalStorage => m_localStorage;

        /// <summary>
        /// Exports, imports, and local storage.
        /// </summary>
        public IEnumerable<KeyValuePair<MyDefinitionId, TradeRequirements>> AllStorage => m_exports.Concat(m_imports).Concat(m_localStorage);
        /// <summary>
        /// Exports and local storage.
        /// </summary>
        public IEnumerable<KeyValuePair<MyDefinitionId, TradeRequirements>> ProductionStorage => m_exports.Concat(m_localStorage);

        public readonly MyDefinitionId? SpecialityExport;
        public readonly ProceduralStationSpeciality Speciality;
        public readonly string Name;
        public readonly int Population;
        public readonly double StorageVolume;
        public readonly double StorageMass;

        public ProceduralConstructionSeed(ProceduralFactionSeed faction, Vector4D locationDepth, Quaternion? orientation, long seed,
            int? populationOverride = null)
        {
            Seed = seed;
            Location = locationDepth.XYZ();
            Faction = faction;
            var tmpRandom = new Random((int)seed);
            Population = populationOverride ?? (int)MyMath.Clamp((float)(Math.Round(5 * tmpRandom.NextExponential() * locationDepth.W)), 1, 100);
            var sqrtPopulation = Math.Sqrt(Population);

            var choice = new WeightedChoice<ProceduralStationSpeciality>();
            foreach (var kv in Faction.Specialities)
                foreach (var kt in kv.Key.StationSpecialities)
                    choice.Add(kt.Key, kv.Value * kt.Value);
            Speciality = choice.Choose(tmpRandom.NextDouble(), WeightedChoice<ProceduralStationSpeciality>.WeightedNormalization.ClampToZero);
            m_exports = new Dictionary<MyDefinitionId, TradeRequirements>(MyDefinitionId.Comparer);
            m_imports = new Dictionary<MyDefinitionId, TradeRequirements>(MyDefinitionId.Comparer);
            List<MyDefinitionBase> rchoice = null;
            var specialExport = tmpRandom.NextDouble() <= Speciality.SpecializationChance;
            if (specialExport)
                rchoice = new List<MyDefinitionBase>();
            foreach (var kv in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (Speciality.CanExport(kv))
                {
                    m_exports.Add(kv.Id, default(TradeRequirements));
                    rchoice?.Add(kv);
                }
                if (Speciality.CanImport(kv))
                    m_imports.Add(kv.Id, default(TradeRequirements));
            }
            SpecialityExport = null;
            if (specialExport)
            {
                // ReSharper disable once PossibleNullReferenceException
                SpecialityExport = rchoice[tmpRandom.Next(0, rchoice.Count)].Id;
                m_exports.Clear();
                m_exports.Add(SpecialityExport.Value, default(TradeRequirements));
            }

            var nameBuilder = new StringBuilder();
            nameBuilder.Append(Faction.FounderName).Append("'s ");
            if (SpecialityExport.HasValue)
                nameBuilder.Append(MyDefinitionManager.Static.GetDefinition(SpecialityExport.Value).DisplayNameText).Append(" ");
            else if (Speciality.GeneralizedPrefixes != null && Speciality.GeneralizedPrefixes.Length > 0)
                nameBuilder.Append(tmpRandom.NextUniformChoice(Speciality.GeneralizedPrefixes)).Append(" ");
            nameBuilder.Append(tmpRandom.NextUniformChoice(Speciality.Suffixes));
            Name = nameBuilder.ToString();

            // Compute hotlisted import amounts
            {
                var keys = m_imports.Keys.ToList();
                foreach (var kv in keys)
                {
                    var baseMult = Population * tmpRandom.NextExponential() / keys.Count;
                    var k = MyDefinitionManager.Static.GetDefinition(kv);
                    var pi = k as MyPhysicalItemDefinition;
                    if (k is MyComponentDefinition)
                        baseMult *= 100 / pi.Mass;
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ore))
                        baseMult *= 10000 / (1 + Math.Sqrt(OreUtilities.GetRarity(k.Id)));
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ingot))
                        baseMult *= 5000 * OreUtilities.GetOutputRatio(k.Id);
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_GasProperties))
                        baseMult *= 250000; // 1/10th a large tank
                    else if (pi != null)
                        baseMult *= 10 / pi.Mass;

                    m_imports[kv] = new TradeRequirements(baseMult, Math.Sqrt(baseMult));
                }
            }

            // Compute exported amounts
            {
                var keys = m_exports.Keys.ToList();
                foreach (var kv in keys)
                {
                    var baseMult = Population * Population * tmpRandom.NextExponential() / keys.Count;
                    var k = MyDefinitionManager.Static.GetDefinition(kv);
                    var pi = k as MyPhysicalItemDefinition;
                    if (k is MyComponentDefinition)
                        baseMult *= 100 / pi.Mass;
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ore))
                        baseMult *= 10000 / (1 + Math.Sqrt(OreUtilities.GetRarity(k.Id)));
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_Ingot))
                        baseMult *= 5000 * OreUtilities.GetOutputRatio(k.Id);
                    else if (k.Id.TypeId == typeof(MyObjectBuilder_GasProperties))
                        baseMult *= 250000; // 1/10th a large tank
                    m_exports[kv] = new TradeRequirements(baseMult, Math.Sqrt(baseMult));
                }
            }

            // Using exports, compute imports
            {
                foreach (var kv in m_exports)
                {
                    var producer = BlueprintIndex.Instance.GetAllProducing(kv.Key, true).ToList();
                    foreach (var blueprint in producer)
                        foreach (var ingredient in blueprint.Ingredients)
                        {
                            var aStorage = kv.Value.Storage * (double)ingredient.Value / producer.Count;
                            var aThroughput = kv.Value.Throughput * (double)ingredient.Value / producer.Count;
                            TradeRequirements current;
                            m_imports[ingredient.Key] = m_imports.TryGetValue(ingredient.Key, out current) ?
                                new TradeRequirements(current.Storage + aStorage, current.Throughput + aThroughput) :
                                new TradeRequirements(aStorage, aThroughput);
                        }
                }
            }

            m_localStorage = new Dictionary<MyDefinitionId, TradeRequirements>(MyDefinitionId.Comparer)
            {
                // ~0.5MWH / person stored, ~1MW / person produced
                [MyResourceDistributorComponent.ElectricityId] = new TradeRequirements(Population * 0.5 * tmpRandom.NextNormal(1, 0.1), Population * tmpRandom.NextNormal(1, 0.1), 10, 10),
                // one large O2 tank / 10 person stored (~2 days), 1 O2/sec/person produced
                [MyResourceDistributorComponent.OxygenId] = new TradeRequirements(Population * 1e4 * tmpRandom.NextNormal(1, 0.1), Population * 1 * tmpRandom.NextNormal(1, 0.1), 10, 10),
                // I don't even know how I'd guess this
                [MyResourceDistributorComponent.HydrogenId] = new TradeRequirements(Population * 1e4 * tmpRandom.NextExponential(), tmpRandom.NextExponential() * sqrtPopulation)
            };

            // Compute mass & volume of storage
            ComputeStorageVolumeMass(out StorageVolume, out StorageMass);

            // ReSharper disable once UseObjectOrCollectionInitializer
            m_blockCountRequirements = new Dictionary<SupportedBlockTypes, BlockRequirement>(SupportedBlockTypesEquality.Instance);
            // living quarters.
            var addlLiving = Faction.AttributeWeight(ProceduralFactionSpeciality.Housing);
            m_blockCountRequirements[SupportedBlockTypes.CryoChamber] = Population + Math.Max(0, (int)Math.Round(sqrtPopulation * (tmpRandom.NextNormal(1, 2) + addlLiving)));
            m_blockCountRequirements[SupportedBlockTypes.MedicalRoom] = new BlockRequirement(Math.Max(1, (int)Math.Round(sqrtPopulation * (tmpRandom.NextNormal(0.5, 0.5) + Math.Max(addlLiving, Faction.AttributeWeight(ProceduralFactionSpeciality.Military))))), 1e3);
            m_blockCountRequirements[SupportedBlockTypes.ShipController] = Math.Max(1, (int)Math.Round(Population * tmpRandom.NextNormal(0.5, 0.5)));
            // how "defensive" this group is
            m_blockCountRequirements[SupportedBlockTypes.Weapon] = Math.Max(0, (int)Math.Round(Population * tmpRandom.NextNormal(2, 2) * Faction.AttributeWeight(ProceduralFactionSpeciality.Military)));
            // ship repair?
            m_blockCountRequirements[SupportedBlockTypes.ShipConstruction] = Math.Max(0, (int)Math.Round(Population * tmpRandom.NextNormal(4, 3) * Faction.AttributeWeight(ProceduralFactionSpeciality.Repair)));
            // docking?
            m_blockCountRequirements[SupportedBlockTypes.Docking] = Math.Max(1, (int)Math.Round(sqrtPopulation * tmpRandom.NextNormal(1, 1) * Faction.AttributeWeight(ProceduralFactionSpeciality.Housing)));
            // comms?
            m_blockCountRequirements[SupportedBlockTypes.Communications] = new BlockRequirement(Math.Max(1, (int)Math.Round(sqrtPopulation * MyMath.Clamp((float)tmpRandom.NextNormal(), 0, 1))), 1e6);

            Orientation = tmpRandom.NextQuaternion();
            // Branched, since we want to consume a quaternion from the random.
            if (orientation != null)
                Orientation = orientation.Value;
        }

        private void ComputeStorageVolumeMass(out double volume, out double mass)
        {
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
            volume = vol.X;
            mass = vol.Y;
        }

        public ProceduralConstructionSeed(ProceduralFactionSeed faction, Vector3D location, Ob_ProceduralConstructionSeed ob)
        {
            Faction = faction;
            Location = location;
            Name = ob.Name;
            Orientation = ob.Orientation;
            Population = ob.Population;
            Seed = ob.Seed;
            Speciality = ob.Speciality;
            SpecialityExport = ob.SpecialityExport;
            m_blockCountRequirements = new Dictionary<SupportedBlockTypes, BlockRequirement>();
            foreach (var k in ob.BlockCountRequirements)
                m_blockCountRequirements[k.Type] = new BlockRequirement(k.Count, k.ErrorMultiplier);
            m_imports = TradeReqToDictionary(ob.Imports);
            m_exports = TradeReqToDictionary(ob.Exports);
            m_localStorage = TradeReqToDictionary(ob.Local);

            ComputeStorageVolumeMass(out StorageVolume, out StorageMass);
        }

        public Ob_ProceduralConstructionSeed GetObjectBuilder()
        {
            return new Ob_ProceduralConstructionSeed()
            {
                BlockCountRequirements = m_blockCountRequirements
                    .Select(
                        x => new Ob_ProceduralConstructionSeed.Ob_BlockCountRequirement()
                        {
                            Type = x.Key,
                            Count = x.Value.Count,
                            ErrorMultiplier = x.Value.Multiplier
                        }).ToList(),
                Exports = TradeReqToList(m_exports),
                Imports = TradeReqToList(m_imports),
                Local = TradeReqToList(m_localStorage),
                Name = Name,
                Orientation =  Orientation,
                Population = Population,
                Seed = Seed,
                Speciality = Speciality,
                SpecialityExport = SpecialityExport,
                FactionSeed = Faction.Seed
            };
        }

        private static List<Ob_ProceduralConstructionSeed.Ob_TradeRequirement> TradeReqToList(
            Dictionary<MyDefinitionId, TradeRequirements> d)
        {
            return d.Select(x => new Ob_ProceduralConstructionSeed.Ob_TradeRequirement()
            {
                Storage = x.Value.Storage,
                StorageErrorMultiplier = x.Value.StorageErrorMultiplier,
                Throughput = x.Value.Throughput,
                ThroughputErrorMultiplier = x.Value.ThroughputErrorMultiplier,
                Type = x.Key
            }).ToList();
        }

        private static Dictionary<MyDefinitionId, TradeRequirements> TradeReqToDictionary(
            IEnumerable<Ob_ProceduralConstructionSeed.Ob_TradeRequirement> list)
        {
            var res = new Dictionary<MyDefinitionId, TradeRequirements>(MyDefinitionId.Comparer);
            foreach (var k in list)
                res[k.Type] = new TradeRequirements(k.Storage, k.Throughput, k.StorageErrorMultiplier, k.ThroughputErrorMultiplier);
            return res;
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

        public struct TradeRequirements
        {

            /// <summary>
            /// Hint used to determine the number of this item we want.
            /// </summary>
            public readonly double Storage;

            /// <summary>
            /// Hint used to choose how many factories we need.
            /// </summary>
            public readonly double Throughput;

            public readonly double ThroughputErrorMultiplier;
            public readonly double StorageErrorMultiplier;

            public TradeRequirements(double storage, double throughput, double storageErrMult = 1, double throughErrMult = 1)
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

        public struct BlockRequirement
        {
            public readonly int Count;
            public readonly double Multiplier;

            public static readonly BlockRequirement Zero = new BlockRequirement(0);

            public BlockRequirement(int c, double mult = 1)
            {
                Count = c;
                Multiplier = mult;
            }

            public static implicit operator BlockRequirement(int value)
            {
                return new BlockRequirement(value);
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

        private readonly Dictionary<SupportedBlockTypes, BlockRequirement> m_blockCountRequirements;

        public IEnumerable<KeyValuePair<SupportedBlockTypes, BlockRequirement>> BlockCountRequirements => m_blockCountRequirements;

        public BlockRequirement BlockCountRequirement(SupportedBlockTypes key)
        {
            return m_blockCountRequirements.GetValueOrDefault(key, BlockRequirement.Zero);
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

    public class Ob_ProceduralConstructionSeed
    {
        public string Name;
        public int Population;
        public long Seed;
        public ulong FactionSeed;

        [XmlElement("Speciality")]
        public string SpecialitySerial
        {
            get { return Speciality?.Name; }
            set { Speciality = ProceduralStationSpeciality.ByName(value); }
        }

        [XmlIgnore]
        public ProceduralStationSpeciality Speciality;

        [XmlIgnore]
        public MyDefinitionId? SpecialityExport = null;

        [XmlElement("SpecialityExport")]
        private DefinitionIdWrapper SpecialityExportWrapper
        {
            get { return SpecialityExport.HasValue ? new DefinitionIdWrapper() { Data = SpecialityExport.Value } : null; }
            set { SpecialityExport = value?.Data; }
        }


        public SerializableQuaternion Orientation;

        [XmlElement("BlockCountRequirement")]
        public List<Ob_BlockCountRequirement> BlockCountRequirements =
            new List<Ob_BlockCountRequirement>();

        [XmlElement("Export")]
        public List<Ob_TradeRequirement> Exports = new List<Ob_TradeRequirement>();

        [XmlElement("Import")]
        public List<Ob_TradeRequirement> Imports = new List<Ob_TradeRequirement>();

        [XmlElement("Local")]
        public List<Ob_TradeRequirement> Local = new List<Ob_TradeRequirement>();

        public class Ob_TradeRequirement
        {
            [XmlElement("Type")]
            public SerializableDefinitionId Type;

            [XmlElement("Storage")]
            [DefaultValue(0)]
            public double Storage = 0;
            [XmlElement("Throughput")]
            [DefaultValue(0)]
            public double Throughput = 0;

            [XmlElement("ThroughputErrorScale")]
            [DefaultValue(1)]
            public double ThroughputErrorMultiplier = 1;
            [XmlElement("StorageErrorScale")]
            [DefaultValue(1)]
            public double StorageErrorMultiplier = 1;
        }

        public class Ob_BlockCountRequirement
        {
            [XmlAttribute("Type")]
            public SupportedBlockTypes Type;

            [XmlAttribute("Count")]
            public int Count;

            [XmlAttribute("ErrorScale")]
            [DefaultValue(1)]
            public double ErrorMultiplier = 1;
        }

        private class DefinitionIdWrapper
        {
            [XmlElement("Id")]
            public SerializableDefinitionId Data;
        }
    }
}