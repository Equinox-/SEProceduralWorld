using System;
using System.Collections.Generic;
using System.Linq;
using ProcBuild.Utils;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;

namespace ProcBuild.Library
{
    public class MyBlockSetInfo
    {
        public readonly Dictionary<MyComponentDefinition, int> ComponentCost = new Dictionary<MyComponentDefinition, int>();
        public readonly Dictionary<MyDefinitionId, int> BlockCountByType = new Dictionary<MyDefinitionId, int>();
        public readonly Dictionary<string, float> PowerConsumptionByGroup = new Dictionary<string, float>();

        private readonly Dictionary<MyDefinitionId, double> m_gasStorageCache = new Dictionary<MyDefinitionId, double>(16);
        private readonly Dictionary<MyDefinitionId, double> m_productionCache = new Dictionary<MyDefinitionId, double>(128);

        public void UpdateCache()
        {
            TotalPowerNetConsumption = PowerConsumptionByGroup.Values.Sum();
            TotalPowerStorage = BlockCountByType
                .Select(x => MyTuple.Create(MyDefinitionManager.Static.GetCubeBlockDefinition(x.Key), x.Value))
                .Where(x => x.Item1 is MyBatteryBlockDefinition)
                .Sum(x => ((MyBatteryBlockDefinition)x.Item1).MaxStoredPower * x.Item2);
            TotalInventoryCapacity = BlockCountByType
                .Select(x => x.Value * MyInventoryUtility.GetInventoryVolume(x.Key))
                .Sum();
            m_productionCache.Clear();
            m_gasStorageCache.Clear();
        }

        public float TotalPowerNetConsumption { get; private set; }

        public double TotalPowerStorage { get; private set; }

        public double TotalInventoryCapacity { get; private set; }

        public double TotalProduction(MyDefinitionId result)
        {
            double val;
            if (m_productionCache.TryGetValue(result, out val)) return val;
            return m_productionCache[result] = ComputeTotalProduction(result);
        }

        public double TotalGasStorage(MyDefinitionId id)
        {
            if (id.Equals(MyResourceDistributorComponent.ElectricityId)) return TotalPowerStorage;
            double val;
            if (m_gasStorageCache.TryGetValue(id, out val)) return val;
            return m_gasStorageCache[id] = ComputeTotalGasStorage(id);
        }

        private double ComputeTotalGasStorage(MyDefinitionId id)
        {
            var storage = 0.0D;
            foreach (var kv in BlockCountByType)
            {
                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(kv.Key) as MyGasTankDefinition;
                if (def == null || !def.StoredGasId.Equals(id)) continue;
                storage += def.Capacity * kv.Value;
            }
            return storage;
        }

        private static double ProductionOfBy(MyBlueprintIndex.MyIndexedBlueprint bp, MyProductionBlockDefinition def)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var test in bp.Blueprints)
            {
                if (!def.BlueprintClasses.Any(x => x.ContainsBlueprint(test.Key))) continue;
                var root = (1.0 / (test.Key.BaseProductionTimeInSeconds * test.Value));
                var refinery = def as MyRefineryDefinition;
                if (refinery != null)
                    root *= refinery.RefineSpeed;
                var assembler = def as MyAssemblerDefinition;
                if (assembler != null)
                    root *= assembler.AssemblySpeed;
                return root;
            }
            return 0;
        }

        private static readonly MyCache<MyTuple<MyDefinitionId, MyProductionBlockDefinition>, double> ProductionOfByCache = new MyCache<MyTuple<MyDefinitionId, MyProductionBlockDefinition>, double>(2048);
        private static double ProductionConsumptionOfBy(MyDefinitionId result, MyProductionBlockDefinition prodDef)
        {
            return ProductionOfByCache.GetOrCreate(MyTuple.Create(result, prodDef), ProductionConsumptionOfBy_Internal);
        }

        private static double ProductionConsumptionOfBy_Internal(MyTuple<MyDefinitionId, MyProductionBlockDefinition> key)
        {
            var result = key.Item1;
            var prodDef = key.Item2;
            var sources = MyBlueprintIndex.Instance.GetAllProducing(result).ToList();
            var consumers = MyBlueprintIndex.Instance.GetAllConsuming(result).ToList();
            return sources.Concat(consumers).Select(opt => ProductionOfBy(opt, prodDef)).FirstOrDefault(prod => prod > double.Epsilon);
        }

        private double ComputeTotalProduction(MyDefinitionId result)
        {
            var production = 0.0D;
            foreach (var kv in BlockCountByType)
            {
                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(kv.Key);
                var oxyDef = def as MyOxygenGeneratorDefinition;
                if (oxyDef != null)
                {
                    if (oxyDef.ProducedGases == null) continue;
                    var bestProd = oxyDef.ProducedGases.
                        Where(recipe => recipe.Id.Equals(result)).
                        Select(recipe => recipe.IceToGasRatio * oxyDef.IceConsumptionPerSecond).DefaultIfEmpty().Max();
                    production += bestProd;
                    continue;
                }

                var oxyFarmDef = def as MyOxygenFarmDefinition;
                if (oxyFarmDef != null)
                {
                    if (oxyFarmDef.ProducedGas.Equals(result))
                        production += oxyFarmDef.MaxGasOutput * MyUtilities.SunMovementMultiplier * (oxyFarmDef.IsTwoSided ? 1 : 0.5f);
                    continue;
                }

                if (def is MyGasTankDefinition)
                    continue;

                var prodDef = def as MyProductionBlockDefinition;
                if (prodDef == null) continue;
                production += ProductionConsumptionOfBy(result, prodDef) * kv.Value;
            }
            return production;
        }

        public void AddToSelf(MyBlockSetInfo other)
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
        }

        public void SubtractFromSelf(MyBlockSetInfo other)
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
        }

        public static MyBlockSetInfo operator +(MyBlockSetInfo a, MyBlockSetInfo b)
        {
            var res = new MyBlockSetInfo();
            res.AddToSelf(a);
            res.AddToSelf(b);
            return res;
        }

        public static MyBlockSetInfo operator -(MyBlockSetInfo a, MyBlockSetInfo b)
        {
            var res = new MyBlockSetInfo();
            res.AddToSelf(a);
            res.SubtractFromSelf(b);
            return res;
        }
    }
}
