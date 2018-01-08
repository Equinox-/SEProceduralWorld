using System;
using Equinox.Utils.Session;
using VRage.Utils;

namespace Equinox.ProceduralWorld.Names
{
    public class ExoticNameGenerator : NameGeneratorBase
    {
        private static readonly string[] VowelPhrase = new[] { "a", "au", "e", "ei", "i", "o", "oe", "ou", "u", "ua", "ue", "uo", "uy", "y" };
        private static readonly string[] ConsonantPhrase = new[]
            {
                "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "q", "qu", "r", "s", "t", "v", "w", "x", "z", "cc", "ch", "tch", "ck", "dge", "gh", "gu", "ng", "ph", "sc", "sch", "sh", "th", "wh", "xh", "bt", "pt", "kn", "gn", "pn",
                "mb", "lm", "ps", "rh", "wr", "ti", "ci", "si", "su", "si", "su"
            };

        public override string Generate(ulong seed)
        {
            var random = new Random((int)seed);
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

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_ExoticNameGenerator;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}",
                    configOriginal.GetType(),
                    GetType());
                return;
            }
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_ExoticNameGenerator();
        }
    }

    public class Ob_ExoticNameGenerator : Ob_NameGeneratorBase
    {
    }
}
