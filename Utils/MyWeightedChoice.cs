using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcBuild.Utils
{
    public class MyWeightedChoice<TK>
    {
        private readonly Dictionary<TK, float> values = new Dictionary<TK, float>();

        public void Add(TK key, float weight)
        {
            float cv = 0;
            values.TryGetValue(key, out cv);
            values[key] = cv + weight;
        }

        public enum WeightedNormalization
        {
            ClampToZero = 0,
            ShiftToZero = 1,
            Exponential = 2
        }

        public TK Choose(double normNoise, WeightedNormalization strat = WeightedNormalization.ShiftToZero)
        {
            var sum = 0.0;
            var min = double.MaxValue;
            foreach (var weight in values.Values)
            {
                switch (strat)
                {
                    case WeightedNormalization.ClampToZero:
                        sum += Math.Max(0, weight);
                        min = 0;
                        break;
                    case WeightedNormalization.Exponential:
                        sum += Math.Exp(weight);
                        break;
                    case WeightedNormalization.ShiftToZero:
                    default:
                        sum += weight;
                        min = Math.Min(min, weight);
                        break;
                }
            }
            if (strat == WeightedNormalization.ShiftToZero)
                sum -= min * values.Count;

            var evalNoise = normNoise * sum;
            var seenNoise = 0.0;

            var best = default(TK);
            var bestWeight = 0.0;
            foreach (var entry in values)
            {
                var weight = entry.Value;

                var weightReal = 0.0;
                switch (strat)
                {
                    case WeightedNormalization.ClampToZero:
                        weightReal = Math.Max(0, weight);
                        break;
                    case WeightedNormalization.Exponential:
                        weightReal = Math.Exp(weight);
                        break;
                    case WeightedNormalization.ShiftToZero:
                    default:
                        weightReal = weight - min;
                        break;
                }
                if (weightReal >= bestWeight)
                {
                    bestWeight = weightReal;
                    best = entry.Key;
                }
                seenNoise += weightReal;
                if (evalNoise <= seenNoise)
                    return entry.Key;
            }
            return best;
        }

        public int Count => values.Count;
    }
}
