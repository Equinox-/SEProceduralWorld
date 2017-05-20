using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using VRage;
using VRage.Game;

namespace ProcBuild.Utils
{
    public class MyBlueprintIndex
    {
        private static MyBlueprintIndex m_instance;

        public static MyBlueprintIndex Instance
        {
            get
            {
                if (m_instance != null) return m_instance;
                m_instance = new MyBlueprintIndex();
                m_instance.Load();
                return m_instance;
            }
        }

        private readonly Dictionary<MyDefinitionId, List<MyIndexedBlueprint>> m_sourceIndex = new Dictionary<MyDefinitionId, List<MyIndexedBlueprint>>();
        private readonly Dictionary<MyDefinitionId, List<MyIndexedBlueprint>> m_index = new Dictionary<MyDefinitionId, List<MyIndexedBlueprint>>();
        private readonly Dictionary<MyDefinitionId, List<MyIndexedBlueprint>> m_topLevelIndex = new Dictionary<MyDefinitionId, List<MyIndexedBlueprint>>();

        public class MyIndexedBlueprint
        {
            public readonly MyDefinitionId Result;
            public readonly Dictionary<MyDefinitionId, MyFixedPoint> Ingredients;
            // Requires (double) iterations of (Definition) to produce/consume this.
            public readonly Dictionary<MyBlueprintDefinitionBase, double> Blueprints = new Dictionary<MyBlueprintDefinitionBase, double>();

            public readonly bool ConsumptionRecipe;

            internal MyIndexedBlueprint(MyBlueprintDefinitionBase def, MyDefinitionId result, MyFixedPoint divider, bool reverse)
            {
                Result = result;
                Ingredients = new Dictionary<MyDefinitionId, MyFixedPoint>();
                ConsumptionRecipe = reverse;
                if (!reverse)
                    foreach (var req in def.Prerequisites)
                        Ingredients[req.Id] = (MyFixedPoint)((double)req.Amount / (double)divider);
                else
                    foreach (var req in def.Results)
                        Ingredients[req.Id] = (MyFixedPoint)((double)req.Amount / (double)divider);
            }

            internal MyIndexedBlueprint(MyDefinitionId result, Dictionary<MyDefinitionId, MyFixedPoint> recipe)
            {
                Result = result;
                Ingredients = recipe;
            }
        }


        private List<MyIndexedBlueprint> GetSafe(MyDefinitionId id)
        {
            List<MyIndexedBlueprint> vals = null;
            if (!m_index.TryGetValue(id, out vals))
                m_index[id] = vals = new List<MyIndexedBlueprint>();
            return vals;
        }

        private List<MyIndexedBlueprint> GetTopLevelSafe(MyDefinitionId id)
        {
            List<MyIndexedBlueprint> vals = null;
            if (!m_topLevelIndex.TryGetValue(id, out vals))
                m_topLevelIndex[id] = vals = new List<MyIndexedBlueprint>();
            return vals;
        }

        private List<MyIndexedBlueprint> GetSourceSafe(MyDefinitionId id)
        {
            List<MyIndexedBlueprint> vals = null;
            if (!m_sourceIndex.TryGetValue(id, out vals))
                m_sourceIndex[id] = vals = new List<MyIndexedBlueprint>();
            return vals;
        }

        public MyIndexedBlueprint GetTopLevel(MyDefinitionId key)
        {
            List<MyIndexedBlueprint> res;
            return m_topLevelIndex.TryGetValue(key, out res) && res != null && res.Count > 0 ? res[0] : null;
        }

        public IEnumerable<MyIndexedBlueprint> GetAllConsuming(MyDefinitionId result)
        {
            List<MyIndexedBlueprint> res;
            return m_sourceIndex.TryGetValue(result, out res) ? res : Enumerable.Empty<MyIndexedBlueprint>();
        }

        public IEnumerable<MyIndexedBlueprint> GetAllProducing(MyDefinitionId result)
        {
            List<MyIndexedBlueprint> res;
            return m_index.TryGetValue(result, out res) ? res : Enumerable.Empty<MyIndexedBlueprint>();
        }

        private void Load()
        {
            var needsElaboration = new Queue<MyIndexedBlueprint>();
            // Load base blueprints.
            foreach (var blueprint in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                foreach (var result in blueprint.Results)
                    if (result.Amount > 0)
                    {
                        var bp = new MyIndexedBlueprint(blueprint, result.Id, result.Amount, false);
                        bp.Blueprints[blueprint] = 1.0 / (double)result.Amount;
                        needsElaboration.Enqueue(bp);
                        GetSafe(result.Id).Add(bp);
                    }
                foreach (var src in blueprint.Prerequisites)
                    if (src.Amount > 0)
                    {
                        var bp = new MyIndexedBlueprint(blueprint, src.Id, src.Amount, true);
                        bp.Blueprints[blueprint] = 1.0 / (double)src.Amount;
                        GetSourceSafe(src.Id).Add(bp);
                    }
            }

            while (needsElaboration.Count > 0)
            {
                var bp = needsElaboration.Dequeue();
                var elaborate = bp.Ingredients.Keys.Any(m_index.ContainsKey);
                if (!elaborate)
                {
                    GetTopLevelSafe(bp.Result).Add(bp);
                    continue;
                }
                var recipe = new Dictionary<MyDefinitionId, MyFixedPoint>();
                var bps = new Dictionary<MyBlueprintDefinitionBase, double>(bp.Blueprints);
                foreach (var kv in bp.Ingredients)
                {
                    List<MyIndexedBlueprint> bpChild;
                    if (!m_index.TryGetValue(kv.Key, out bpChild))
                    {
                        recipe.AddValue(kv.Key, kv.Value);
                        continue;
                    }
                    if (bpChild == null || bpChild.Count <= 0) continue;
                    // Something better than the last one?
                    var bpSrc = bpChild[bpChild.Count - 1];
                    foreach (var x in bpSrc.Blueprints)
                        bps.AddValue(x.Key, x.Value * (double)kv.Value);
                    foreach (var kvC in bpSrc.Ingredients)
                    {
                        recipe.AddValue(kvC.Key, kvC.Value * kv.Value);
                    }
                }
                var bpOut = new MyIndexedBlueprint(bp.Result, recipe);
                foreach (var kv in bps)
                    bpOut.Blueprints[kv.Key] = kv.Value;
                needsElaboration.Enqueue(bpOut);
                GetSafe(bp.Result).Add(bpOut);
            }
        }
    }
}
