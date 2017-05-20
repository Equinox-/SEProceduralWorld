using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="normNoise">Noise to apply</param>
        /// <param name="quantileFifty">The quantile mapped to 0.5 noise</param>
        /// <returns></returns>
        public TK ChooseByQuantile(double normNoise, double quantileFifty)
        {
            var list = new List<KeyValuePair<TK, float>>(values);
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            // swap equal weight items randomly.
            var j = 0L;
            for (var i = 0; i < list.Count - 1; i++)
            {
                if (!(Math.Abs(list[i].Value - list[i + 1].Value) < float.Epsilon)) continue;
                if (((long)(normNoise * long.MaxValue) & j) != 0)
                {
                    var tmp = list[i];
                    list[i] = list[i + 1];
                    list[i + 1] = tmp;
                }
                j = (j * 2) + 2;
            }

            // (0.5)^x == quantileFifty
            var exponent = Math.Log(quantileFifty) / Math.Log(0.5);
            var res = Math.Pow(normNoise, exponent);
            return list[(int)MyMath.Clamp((float)res * list.Count, 0, list.Count - 1)].Key;
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
