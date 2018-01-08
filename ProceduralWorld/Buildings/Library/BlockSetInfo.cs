using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    /// <summary>
    /// Not threadsafe.  Really really.
    /// </summary>
    public class BlockSetInfo
    {
        public readonly Dictionary<MyComponentDefinition, int> ComponentCost = new Dictionary<MyComponentDefinition, int>();
        public readonly Dictionary<MyDefinitionId, int> BlockCountByType = new Dictionary<MyDefinitionId, int>(MyDefinitionId.Comparer);
        public readonly Dictionary<string, float> PowerConsumptionByGroup = new Dictionary<string, float>();

        private readonly Dictionary<MyDefinitionId, double> m_gasStorageCache = new Dictionary<MyDefinitionId, double>(16, MyDefinitionId.Comparer);
        private readonly Dictionary<MyDefinitionId, double> m_productionCache = new Dictionary<MyDefinitionId, double>(128, MyDefinitionId.Comparer);

        internal void UpdateCache()
        {
            TotalPowerNetConsumption = PowerConsumptionByGroup.Values.Sum();
            TotalPowerStorage = BlockCountByType
                .Select(x => MyTuple.Create(MyDefinitionManager.Static.GetCubeBlockDefinition(x.Key), x.Value))
                .Where(x => x.Item1 is MyBatteryBlockDefinition)
                .Sum(x => ((MyBatteryBlockDefinition)x.Item1).MaxStoredPower * x.Item2);
            TotalInventoryCapacity = BlockCountByType
                .Select(x => x.Value * InventoryUtility.GetInventoryVolume(x.Key))
                .Sum();

            m_productionCache.Clear();
            m_gasStorageCache.Clear();
            foreach (var kv in BlockCountByType)
            {
                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(kv.Key);
                var gasTankDef = def as MyGasTankDefinition;
                if (gasTankDef != null)
                {
                    m_gasStorageCache.AddValue(gasTankDef.StoredGasId, gasTankDef.Capacity * kv.Value);
                    continue;
                }
                var oxyDef = def as MyOxygenGeneratorDefinition;
                if (oxyDef != null)
                {
                    if (oxyDef.ProducedGases == null) continue;
                    foreach (var recipe in oxyDef.ProducedGases)
                        m_productionCache.AddValue(recipe.Id, recipe.IceToGasRatio * oxyDef.IceConsumptionPerSecond * kv.Value);
                    continue;
                }

                var oxyFarmDef = def as MyOxygenFarmDefinition;
                if (oxyFarmDef != null)
                {
                    m_productionCache.AddValue(oxyFarmDef.ProducedGas, oxyFarmDef.MaxGasOutput * Utilities.SunMovementMultiplier * (oxyFarmDef.IsTwoSided ? 1 : 0.5f) * kv.Value);
                    continue;
                }

                var prodDef = def as MyProductionBlockDefinition;
                if (prodDef == null) continue;
                var speedMult = 1.0;
                var asmDef = def as MyAssemblerDefinition;
                if (asmDef != null)
                    speedMult = asmDef.AssemblySpeed;
                var refineDef = def as MyRefineryDefinition;
                if (refineDef != null)
                    speedMult = refineDef.RefineSpeed;
                foreach (var bpc in prodDef.BlueprintClasses)
                    foreach (var bp in bpc)
                    {
                        foreach (var result in bp.Results)
                            m_productionCache.AddValue(result.Id, speedMult * (1 / bp.BaseProductionTimeInSeconds) * (double)result.Amount * kv.Value);
                        foreach (var preq in bp.Prerequisites)
                        {
                            if (preq.Id.TypeId != typeof(MyObjectBuilder_Ore)) continue;
                            m_productionCache.AddValue(preq.Id, speedMult * (1 / bp.BaseProductionTimeInSeconds) * (double)preq.Amount * kv.Value);
                        }
                    }
            }

            var reqResources = 0D;
            foreach (var kv in BlockCountByType)
            {
                reqResources += BlueprintIndex.Instance.GetRawResourcesFor(kv.Key) * kv.Value;
            }
            TotalRawResources = reqResources;
        }

        /// <summary>
        /// Total required ore, in kg
        /// </summary>
        public double TotalRawResources { get; private set; }

        public float TotalPowerNetConsumption { get; private set; }

        public double TotalPowerStorage { get; private set; }

        public double TotalInventoryCapacity { get; private set; }

        public double TotalProduction(MyDefinitionId result)
        {
            return m_productionCache.GetValueOrDefault(result, 0);
        }

        public double TotalGasStorage(MyDefinitionId id)
        {
            if (id.Equals(MyResourceDistributorComponent.ElectricityId))
                return TotalPowerStorage;
            return m_gasStorageCache.GetValueOrDefault(id, 0);
        }

        public void AddToSelf(BlockSetInfo other)
        {
            foreach (var kv in other.ComponentCost)
                ComponentCost.AddValue(kv.Key, kv.Value);
            foreach (var kv in other.BlockCountByType)
                BlockCountByType.AddValue(kv.Key, kv.Value);
            foreach (var kv in other.PowerConsumptionByGroup)
                PowerConsumptionByGroup.AddValue(kv.Key, kv.Value);

            TotalPowerStorage += other.TotalPowerStorage;
            TotalInventoryCapacity += other.TotalInventoryCapacity;
            TotalPowerNetConsumption += other.TotalPowerNetConsumption;
            foreach (var kv in other.m_gasStorageCache)
                m_gasStorageCache.AddValue(kv.Key, kv.Value);
            foreach (var kv in other.m_productionCache)
                m_productionCache.AddValue(kv.Key, kv.Value);
            TotalRawResources += other.TotalRawResources;
        }

        public void SubtractFromSelf(BlockSetInfo other)
        {
            foreach (var kv in other.ComponentCost)
                ComponentCost.AddValue(kv.Key, -kv.Value);
            foreach (var kv in other.BlockCountByType)
                BlockCountByType.AddValue(kv.Key, -kv.Value);
            foreach (var kv in other.PowerConsumptionByGroup)
                PowerConsumptionByGroup.AddValue(kv.Key, -kv.Value);


            TotalPowerStorage -= other.TotalPowerStorage;
            TotalInventoryCapacity -= other.TotalInventoryCapacity;
            TotalPowerNetConsumption -= other.TotalPowerNetConsumption;
            foreach (var kv in other.m_gasStorageCache)
                m_gasStorageCache.AddValue(kv.Key, -kv.Value);
            foreach (var kv in other.m_productionCache)
                m_productionCache.AddValue(kv.Key, -kv.Value);
            TotalRawResources -= other.TotalRawResources;
        }

        public static BlockSetInfo operator +(BlockSetInfo a, BlockSetInfo b)
        {
            var res = new BlockSetInfo();
            res.AddToSelf(a);
            res.AddToSelf(b);
            return res;
        }

        public static BlockSetInfo operator -(BlockSetInfo a, BlockSetInfo b)
        {
            var res = new BlockSetInfo();
            res.AddToSelf(a);
            res.SubtractFromSelf(b);
            return res;
        }
    }
}
