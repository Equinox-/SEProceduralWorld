using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Generation;
using ProcBuild.Utils;
using Sandbox.ModAPI;
using VRageMath;

namespace ProcBuild.Storage
{
    public class MyProceduralFactionSeed
    {
        public readonly long Seed;
        private readonly Random m_random;
        public readonly string Name;
        public readonly string Tag;

        private static string StripVowels(string a)
        {
            var outv = new StringBuilder(a.Length);
            foreach (var c in a)
                if (c != 'a' && c != 'e' && c != 'u' && c != 'y' && c != 'o')
                    outv.Append(c);
            return outv.ToString();
        }

        private static IEnumerable<string> SelectTagsFrom(string name, int offset, int count)
        {
            // Select all n letter combos from name, maintaining order.
            if (count == 0)
                yield return "";
            else if (count >= name.Length - offset)
                yield return name.Substring(offset);
            else
                for (var i = offset; i <= name.Length - count; i++)
                    foreach (var extra in SelectTagsFrom(name, i + 1, count - 1))
                        yield return name[i] + extra;
        }

        private static string SelectTag(string name)
        {
            foreach (var tag in SelectTagsFrom(StripVowels(name), 0, 3))
                if (!MyAPIGateway.Session.Factions.FactionTagExists(tag))
                    return tag;
            foreach (var tag in SelectTagsFrom(name, 0, 3))
                if (!MyAPIGateway.Session.Factions.FactionTagExists(tag))
                    return tag;
            throw new Exception("Unable to find a tag for " + name);
        }

        public MyProceduralFactionSeed(long seed)
        {
            Seed = seed;
            m_random = new Random((int)seed);

            HueRotation = (float)m_random.NextDouble();
            SaturationModifier = MyMath.Clamp((float)m_random.NextNormal(), -1, 1);
            ValueModifier = MyMath.Clamp((float)m_random.NextNormal(), -1, 1);

            Name = MyNameGenerator.GenerateName(m_random.Next());
            Name = Name.Substring(0, 1).ToUpper() + Name.Substring(1).ToLower();
            Tag = SelectTag(Name).ToUpper();

            // Think: Weaponry and defenses
            Militaristic = MyMath.Clamp((float)m_random.NextNormal(0.5, 0.5), 0, 2);
            // Think: Providing trade centers
            Commercialistic = MyMath.Clamp((float)m_random.NextNormal(0.5, 0.5), 0, 2);
            // Think: Providing repair services and housing
            Services = MyMath.Clamp((float)m_random.NextNormal(0.5, 0.5), 0, 2);
        }

        // 0 to 1
        public readonly float HueRotation;
        // -1 to 1
        public readonly float SaturationModifier;
        // -1 to 1
        public readonly float ValueModifier;

        // General faction attributes
        public readonly float Militaristic, Commercialistic, Services;
    }
}
