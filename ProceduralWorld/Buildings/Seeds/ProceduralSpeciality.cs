using System.Collections.Generic;
using Equinox.Utils.Definitions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class ProceduralStationSpeciality
    {
        private static readonly string[] ShopSuffixes = { "Shop", "Emporium", "Center", "Depot" };

        private readonly DefinitionTester m_exports, m_imports;

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

        private static readonly Dictionary<string, ProceduralStationSpeciality> m_lookupTable =
            new Dictionary<string, ProceduralStationSpeciality>();

        public static ProceduralStationSpeciality ByName(string name)
        {
            return m_lookupTable.GetValueOrDefault(name, null);
        }

        private ProceduralStationSpeciality(string name, DefinitionTester exports, double prependSingleItem, string[] generalizedPrefixes, string[] suffixes, DefinitionTester imports = null)
        {
            m_lookupTable[name] = this;
            Name = name;
            m_exports = exports;
            m_imports = imports;
            SpecializationChance = prependSingleItem;
            GeneralizedPrefixes = generalizedPrefixes;
            Suffixes = suffixes;
        }

        private ProceduralStationSpeciality(string name, DefinitionTester exports, string[] names, DefinitionTester imports = null)
        {
            Name = name;
            m_exports = exports;
            m_imports = imports;
            SpecializationChance = 0;
            GeneralizedPrefixes = null;
            Suffixes = names;
        }

        private static readonly DefinitionFilter AcceptFilter = new DefinitionFilter().OrType(typeof(MyObjectBuilder_GasProperties)).OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_HandDrill), typeof(MyObjectBuilder_GasContainerObject), typeof(MyObjectBuilder_OxygenContainerObject)).
            OrType(typeof(MyObjectBuilder_Ore), typeof(MyObjectBuilder_Ingot), typeof(MyObjectBuilder_Component));
        private static readonly DefinitionFilter NothingFilter = new DefinitionFilter();
        private static readonly DefinitionFilter WeaponryFilter = new DefinitionFilter().OrType(typeof(MyObjectBuilder_AmmoMagazine), typeof(MyObjectBuilder_AutomaticRifle));
        private static readonly DefinitionFilter OxygenAndFuelFilter = new DefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Oxygen").OrTypeSubtype(typeof(MyObjectBuilder_Ingot), "Uranium");
        private static readonly DefinitionTester BuildingEquipment = new DefinitionFilter().OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_Component));

        // Mining asteroids
        public static readonly ProceduralStationSpeciality MiningAbundant = new ProceduralStationSpeciality(nameof(MiningAbundant), new DefinitionFilter().OrTypeTester(typeof(MyObjectBuilder_Ore), x => OreUtilities.GetRarity(x.Id) <= 0.75), new[] { "Ores", "Minerals", "Resources", "Materials" });
        public static readonly ProceduralStationSpeciality MiningRares = new ProceduralStationSpeciality(nameof(MiningRares), new DefinitionFilter().OrTypeTester(typeof(MyObjectBuilder_Ore), x => OreUtilities.GetRarity(x.Id) >= 0.65), new[] { "Rare Ores", "Rare Minerals" });
        // Gas production
        public static readonly ProceduralStationSpeciality HydrogenFuelStation = new ProceduralStationSpeciality(nameof(HydrogenFuelStation), new DefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Hydrogen"), new[] { "Hydrogen Station", "Fuel Depot", "Fuel Station" });
        public static readonly ProceduralStationSpeciality UraniumFuelStation = new ProceduralStationSpeciality(nameof(UraniumFuelStation), new DefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_Ingot), "Uranium"), new[] { "Uranium Depot", "Fuel Depot", "Fuel Station" });
        public static readonly ProceduralStationSpeciality OxygenStation = new ProceduralStationSpeciality(nameof(OxygenStation), new DefinitionFilter().OrTypeSubtype(typeof(MyObjectBuilder_GasProperties), "Oxygen"), new[] { "Oxygen Station", "Life Support" });
        // Ore refinery
        public static readonly ProceduralStationSpeciality Refinery = new ProceduralStationSpeciality(nameof(Refinery), new DefinitionFilter().OrType(typeof(MyObjectBuilder_Ingot)), 0.5, new[] { "" }, new[] { "Refinery", "Smeltery", "Forge" });
        // Assembly
        public static readonly ProceduralStationSpeciality AssemblyWeaponsAmmo = new ProceduralStationSpeciality(nameof(AssemblyWeaponsAmmo), WeaponryFilter, 0.1, new[] { "Weapon", "Ammo", "Military", "Munitions" }, ShopSuffixes);
        public static readonly ProceduralStationSpeciality AssemblyTools = new ProceduralStationSpeciality(nameof(AssemblyTools), new DefinitionFilter().OrType(typeof(MyObjectBuilder_Welder), typeof(MyObjectBuilder_AngleGrinder), typeof(MyObjectBuilder_HandDrill), typeof(MyObjectBuilder_GasContainerObject), typeof(MyObjectBuilder_OxygenContainerObject)), 0.1, new[] { "Tool" }, ShopSuffixes);
        public static readonly ProceduralStationSpeciality AssemblyComponents = new ProceduralStationSpeciality(nameof(AssemblyComponents), new DefinitionFilter().OrType(typeof(MyObjectBuilder_Component)), 0.2, new[] { "Component", "Part" }, ShopSuffixes);
        // Repair
        public static readonly ProceduralStationSpeciality ShipRepair = new ProceduralStationSpeciality(nameof(ShipRepair), BuildingEquipment, new[] { "Repair Yard", "Repair Shop", "Junk Yard" });
        public static readonly ProceduralStationSpeciality ShipConstruction = new ProceduralStationSpeciality(nameof(ShipConstruction), BuildingEquipment, new[] { "Shipyard", "Construction Outpost", "Builder Shop" });
        // Military
        public static readonly ProceduralStationSpeciality DefenseStation = new ProceduralStationSpeciality(nameof(DefenseStation), NothingFilter, new[] { "Military Outpost", "Defense Outpost" }, imports: new DefinitionFilter().Append(OxygenAndFuelFilter).Append(WeaponryFilter));
        // Housing
        public static readonly ProceduralStationSpeciality Housing = new ProceduralStationSpeciality(nameof(Housing), new DefinitionFilter().OrType(typeof(MyObjectBuilder_OxygenContainerObject)), new[] { "Hovels", "Shacks", "Apartments" }, imports: OxygenAndFuelFilter);
        public static readonly ProceduralStationSpeciality LuxuryHousing = new ProceduralStationSpeciality(nameof(LuxuryHousing), new DefinitionFilter().OrType(typeof(MyObjectBuilder_OxygenContainerObject)), new[] { "Condo", "Villa", "Luxury Hotel" }, imports: OxygenAndFuelFilter);
        public static readonly ProceduralStationSpeciality Trading = new ProceduralStationSpeciality(nameof(Trading), AcceptFilter, new[] { "Trading Outpost", "Exchange", "Junk Emporium" }, imports: AcceptFilter);

        public override string ToString()
        {
            return Name;
        }
    }

    public class ProceduralFactionSpeciality
    {
        public string Description { get; }
        public string[] FactionTags { get; }
        public string Name { get; }
        public List<KeyValuePair<ProceduralStationSpeciality, float>> StationSpecialities { get; }
        private static readonly Dictionary<string, ProceduralFactionSpeciality> m_lookupTable =
            new Dictionary<string, ProceduralFactionSpeciality>();

        public static IEnumerable<ProceduralFactionSpeciality> Values => m_lookupTable.Values;

        public static ProceduralFactionSpeciality ByName(string name)
        {
            return m_lookupTable.GetValueOrDefault(name, null);
        }
        private ProceduralFactionSpeciality(string name, string desc, string[] factionTags)
        {
            m_lookupTable[name] = this;
            Name = name;
            Description = desc;
            FactionTags = factionTags;
            StationSpecialities = new List<KeyValuePair<ProceduralStationSpeciality, float>>();
        }

        private ProceduralFactionSpeciality Add(ProceduralStationSpeciality s, float w)
        {
            StationSpecialities.Add(new KeyValuePair<ProceduralStationSpeciality, float>(s, w));
            return this;
        }
        
        public static readonly ProceduralFactionSpeciality Military = new ProceduralFactionSpeciality(nameof(Military), "military operations", new[] { "Military Endeavours", "Mercenaries", "Defenses" }).
            Add(ProceduralStationSpeciality.AssemblyWeaponsAmmo, 0.25f).Add(ProceduralStationSpeciality.DefenseStation, 0.75f);
        public static readonly ProceduralFactionSpeciality Repair = new ProceduralFactionSpeciality(nameof(Repair), "repairing and constructing ships", new[] { "Repair Shops", "Shipyards", "Constructors", "Maintenance" }).
            Add(ProceduralStationSpeciality.ShipConstruction, 0.1f).Add(ProceduralStationSpeciality.ShipRepair, 0.9f);
        public static readonly ProceduralFactionSpeciality Refining = new ProceduralFactionSpeciality(nameof(Refining), "refining ores", new[] { "Refinement", "Processing" }).
            Add(ProceduralStationSpeciality.HydrogenFuelStation, .1f).Add(ProceduralStationSpeciality.OxygenStation, .05f).Add(ProceduralStationSpeciality.Refinery, .85f);
        public static readonly ProceduralFactionSpeciality Assembling = new ProceduralFactionSpeciality(nameof(Assembling), "assembling parts", new[] { "Parts", "Assembly" }).
            Add(ProceduralStationSpeciality.AssemblyComponents, .85f).Add(ProceduralStationSpeciality.AssemblyTools, .1f).Add(ProceduralStationSpeciality.AssemblyWeaponsAmmo, .05f);
        public static readonly ProceduralFactionSpeciality Housing = new ProceduralFactionSpeciality(nameof(Housing), "housing", new[] { "Hotels", "Condos", "Housing" }).
            Add(ProceduralStationSpeciality.Housing, .99f).Add(ProceduralStationSpeciality.LuxuryHousing, .01f);
        public static readonly ProceduralFactionSpeciality Trading = new ProceduralFactionSpeciality(nameof(Trading), "trading", new[] { "Trading", "Exchange" }).
            Add(ProceduralStationSpeciality.Trading, 1);
        public static readonly ProceduralFactionSpeciality Mining = new ProceduralFactionSpeciality(nameof(Mining), "mining", new[] { "Ores", "mining" }).
            Add(ProceduralStationSpeciality.MiningRares, .1f).Add(ProceduralStationSpeciality.MiningAbundant, .9f);
    }
}