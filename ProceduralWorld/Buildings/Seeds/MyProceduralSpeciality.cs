using System.Collections.Generic;
using Equinox.ProceduralWorld.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class MyProceduralStationSpeciality
    {
        private static readonly string[] ShopSuffixes = { "Shop", "Emporium", "Center", "Depot" };

        private readonly MyDefinitionTester m_exports, m_imports;

        public double SpecializationChance { get; }
        public string[] GeneralizedPrefixes { get; }
        public string[] Suffixes { get; }

        public bool CanExport(MyDefinitionBase b)
        {
            return m_exports?.Invoke(b) ?? true;
        }
        public bool CanImport(MyDefinitionBase b)
        {
            return m_imports?.Invoke(b) ?? false;
        }

        public string Name { get; }

        private static readonly Dictionary<string, MyProceduralStationSpeciality> m_lookupTable =
            new Dictionary<string, MyProceduralStationSpeciality>();

        public static MyProceduralStationSpeciality ByName(string name)
        {
            return m_lookupTable.GetValueOrDefault(name, null);
        }

        private MyProceduralStationSpeciality(string name, MyDefinitionTester exports, double prependSingleItem, string[] generalizedPrefixes, string[] suffixes, MyDefinitionTester imports = null)
        {
            m_lookupTable[name] = this;
            Name = name;
            m_exports = exports;
            m_imports = imports;
            SpecializationChance = prependSingleItem;
            GeneralizedPrefixes = generalizedPrefixes;
            Suffixes = suffixes;
        }

        private MyProceduralStationSpeciality(string name, MyDefinitionTester exports, string[] names, MyDefinitionTester imports = null)
        {
            Name = name;
            m_exports = exports;
            m_imports = imports;
            SpecializationChance = 0;
            GeneralizedPrefixes = null;
            Suffixes = names;
        }

        private static readonly MyDefinitionFilter AcceptFilter = new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_GasProperties)).OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_HandDrill), typeof(MyObjectBuilder_GasContainerObject), typeof(MyObjectBuilder_OxygenContainerObject)).
            OrType(typeof(MyObjectBuilder_Ore), typeof(MyObjectBuilder_Ingot), typeof(MyObjectBuilder_Component));
        private static readonly MyDefinitionFilter NothingFilter = new MyDefinitionFilter();
        private static readonly MyDefinitionFilter WeaponryFilter = new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_AmmoMagazine), typeof(MyObjectBuilder_AutomaticRifle));
        private static readonly MyDefinitionFilter OxygenAndFuelFilter = new MyDefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Oxygen").OrTypeSubtype(typeof(MyObjectBuilder_Ingot), "Uranium");
        private static readonly MyDefinitionTester BuildingEquipment = new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_Component));

        // Mining asteroids
        public static readonly MyProceduralStationSpeciality MiningAbundant = new MyProceduralStationSpeciality(nameof(MiningAbundant), new MyDefinitionFilter().OrTypeTester(typeof(MyObjectBuilder_Ore), x => MyOreUtilities.GetRarity(x.Id) <= 0.75), new[] { "Ores", "Minerals", "Resources", "Materials" });
        public static readonly MyProceduralStationSpeciality MiningRares = new MyProceduralStationSpeciality(nameof(MiningRares), new MyDefinitionFilter().OrTypeTester(typeof(MyObjectBuilder_Ore), x => MyOreUtilities.GetRarity(x.Id) >= 0.65), new[] { "Rare Ores", "Rare Minerals" });
        // Gas production
        public static readonly MyProceduralStationSpeciality HydrogenFuelStation = new MyProceduralStationSpeciality(nameof(HydrogenFuelStation), new MyDefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Hydrogen"), new[] { "Hydrogen Station", "Fuel Depot", "Fuel Station" });
        public static readonly MyProceduralStationSpeciality UraniumFuelStation = new MyProceduralStationSpeciality(nameof(UraniumFuelStation), new MyDefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_Ingot), "Uranium"), new[] { "Uranium Depot", "Fuel Depot", "Fuel Station" });
        public static readonly MyProceduralStationSpeciality OxygenStation = new MyProceduralStationSpeciality(nameof(OxygenStation), new MyDefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Oxygen"), new[] { "Oxygen Station", "Life Support" });
        // Ore refinery
        public static readonly MyProceduralStationSpeciality Refinery = new MyProceduralStationSpeciality(nameof(Refinery), new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_Ingot)), 0.5, new[] { "" }, new[] { "Refinery", "Smeltery", "Forge" });
        // Assembly
        public static readonly MyProceduralStationSpeciality AssemblyWeaponsAmmo = new MyProceduralStationSpeciality(nameof(AssemblyWeaponsAmmo), WeaponryFilter, 0.1, new[] { "Weapon", "Ammo", "Military", "Munitions" }, ShopSuffixes);
        public static readonly MyProceduralStationSpeciality AssemblyTools = new MyProceduralStationSpeciality(nameof(AssemblyTools), new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_HandDrill), typeof(MyObjectBuilder_GasContainerObject), typeof(MyObjectBuilder_OxygenContainerObject)), 0.1, new[] { "Tool" }, ShopSuffixes);
        public static readonly MyProceduralStationSpeciality AssemblyComponents = new MyProceduralStationSpeciality(nameof(AssemblyComponents), new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_Component)), 0.2, new[] { "Component", "Part" }, ShopSuffixes);
        // Repair
        public static readonly MyProceduralStationSpeciality ShipRepair = new MyProceduralStationSpeciality(nameof(ShipRepair), BuildingEquipment, new[] { "Repair Yard", "Repair Shop", "Junk Yard" });
        public static readonly MyProceduralStationSpeciality ShipConstruction = new MyProceduralStationSpeciality(nameof(ShipConstruction), BuildingEquipment, new[] { "Shipyard", "Construction Outpost", "Builder Shop" });
        // Military
        public static readonly MyProceduralStationSpeciality DefenseStation = new MyProceduralStationSpeciality(nameof(DefenseStation), NothingFilter, new[] { "Military Outpost", "Defense Outpost" }, imports: new MyDefinitionFilter().Append(OxygenAndFuelFilter).Append(WeaponryFilter));
        // Housing
        public static readonly MyProceduralStationSpeciality Housing = new MyProceduralStationSpeciality(nameof(Housing), new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_OxygenContainerObject)), new[] { "Hovels", "Shacks", "Apartments" }, imports: OxygenAndFuelFilter);
        public static readonly MyProceduralStationSpeciality LuxuryHousing = new MyProceduralStationSpeciality(nameof(LuxuryHousing), new MyDefinitionFilter().OrType(typeof(MyObjectBuilder_OxygenContainerObject)), new[] { "Condo", "Villa", "Luxury Hotel" }, imports: OxygenAndFuelFilter);
        public static readonly MyProceduralStationSpeciality Trading = new MyProceduralStationSpeciality(nameof(Trading), AcceptFilter, new[] { "Trading Outpost", "Exchange", "Junk Emporium" }, imports: AcceptFilter);

        public override string ToString()
        {
            return Name;
        }
    }

    public class MyProceduralFactionSpeciality
    {
        public string Description { get; }
        public string[] FactionTags { get; }
        public string Name { get; }
        public List<KeyValuePair<MyProceduralStationSpeciality, float>> StationSpecialities { get; }
        private static readonly Dictionary<string, MyProceduralFactionSpeciality> m_lookupTable =
            new Dictionary<string, MyProceduralFactionSpeciality>();

        public static IEnumerable<MyProceduralFactionSpeciality> Values => m_lookupTable.Values;

        public static MyProceduralFactionSpeciality ByName(string name)
        {
            return m_lookupTable.GetValueOrDefault(name, null);
        }
        private MyProceduralFactionSpeciality(string name, string desc, string[] factionTags)
        {
            m_lookupTable[name] = this;
            Name = name;
            Description = desc;
            FactionTags = factionTags;
            StationSpecialities = new List<KeyValuePair<MyProceduralStationSpeciality, float>>();
        }

        private MyProceduralFactionSpeciality Add(MyProceduralStationSpeciality s, float w)
        {
            StationSpecialities.Add(new KeyValuePair<MyProceduralStationSpeciality, float>(s, w));
            return this;
        }
        
        public static readonly MyProceduralFactionSpeciality Military = new MyProceduralFactionSpeciality(nameof(Military), "military operations", new[] { "Military Endeavours", "Mercenaries", "Defenses" }).
            Add(MyProceduralStationSpeciality.AssemblyWeaponsAmmo, 0.25f).Add(MyProceduralStationSpeciality.DefenseStation, 0.75f);
        public static readonly MyProceduralFactionSpeciality Repair = new MyProceduralFactionSpeciality(nameof(Repair), "repairing and constructing ships", new[] { "Repair Shops", "Shipyards", "Constructors", "Maintenance" }).
            Add(MyProceduralStationSpeciality.ShipConstruction, 0.1f).Add(MyProceduralStationSpeciality.ShipRepair, 0.9f);
        public static readonly MyProceduralFactionSpeciality Refining = new MyProceduralFactionSpeciality(nameof(Refining), "refining ores", new[] { "Refinement", "Processing" }).
            Add(MyProceduralStationSpeciality.HydrogenFuelStation, .1f).Add(MyProceduralStationSpeciality.OxygenStation, .05f).Add(MyProceduralStationSpeciality.Refinery, .85f);
        public static readonly MyProceduralFactionSpeciality Assembling = new MyProceduralFactionSpeciality(nameof(Assembling), "assembling parts", new[] { "Parts", "Assembly" }).
            Add(MyProceduralStationSpeciality.AssemblyComponents, .85f).Add(MyProceduralStationSpeciality.AssemblyTools, .1f).Add(MyProceduralStationSpeciality.AssemblyWeaponsAmmo, .05f);
        public static readonly MyProceduralFactionSpeciality Housing = new MyProceduralFactionSpeciality(nameof(Housing), "housing", new[] { "Hotels", "Condos", "Housing" }).
            Add(MyProceduralStationSpeciality.Housing, .99f).Add(MyProceduralStationSpeciality.LuxuryHousing, .01f);
        public static readonly MyProceduralFactionSpeciality Trading = new MyProceduralFactionSpeciality(nameof(Trading), "trading", new[] { "Trading", "Exchange" }).
            Add(MyProceduralStationSpeciality.Trading, 1);
        public static readonly MyProceduralFactionSpeciality Mining = new MyProceduralFactionSpeciality(nameof(Mining), "mining", new[] { "Ores", "mining" }).
            Add(MyProceduralStationSpeciality.MiningRares, .1f).Add(MyProceduralStationSpeciality.MiningAbundant, .9f);
    }
}