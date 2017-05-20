using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Storage;

namespace ProcBuild.Generation
{
    public static class MyNameGenerator
    {
        public static string GetName(this MyProceduralRoom module)
        {
            return "M" + module.GetHashCode().ToString("X");
        }

        public static string GetName(this MyProceduralConstruction construction)
        {
            return "C" + construction.GetHashCode().ToString("X");
        }
        
        private static readonly string[] VowelPhrase = new[] { "a", "au", "e", "ei", "i", "o", "oe", "ou", "u", "ua", "ue", "uo", "uy", "y" };
        private static readonly string[] ConsonantPhrase = new[]
            {
                "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "qu", "r", "s", "t", "v", "w", "x", "z", "cc", "ch", "tch", "ck", "dge", "gh", "gu", "ng", "ph", "sc", "sch", "sh", "th", "wh", "xh", "bt", "pt", "kn", "gn", "pn",
                "mb", "lm", "ps", "rh", "wr", "ti", "ci", "si", "su", "si", "su"
            };
        public static string GenerateName(int seed)
        {
            var random = new Random(seed);
            var len = random.Next(5, 10);
            var name = ConsonantPhrase[random.Next(0, ConsonantPhrase.Length)];
            while (name.Length < len)
            {
                var vp = VowelPhrase[random.Next(0, VowelPhrase.Length)];
                for (var j = random.Next(1, 3); (vp.Length > 1 || vp[0] == name[name.Length - 1]) && j > 0; j--)
                    vp = VowelPhrase[random.Next(0, VowelPhrase.Length)];
                name += vp;
                if (random.Next(4) == 0) continue;
                var cp = ConsonantPhrase[random.Next(0, ConsonantPhrase.Length)];
                for (var j = random.Next(1, 3); (cp.Length > 1 || cp[0] == name[name.Length - 1]) && j > 0; j--)
                    cp = ConsonantPhrase[random.Next(0, ConsonantPhrase.Length)];
                name += cp;
            }
            return name;
        }
    }
}
