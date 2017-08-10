using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Utils;
using Equinox.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{

    public class MyProceduralFactionSeed
    {
        public static readonly string[] FactionSuffixes = { "Inc.", "LLC.", "Co.", "Itpl.", "Total", "United" };
        public static readonly string[] Adjectives = { "Dreamy", "Amazing", "World Famous", "General",  };

        public readonly ulong Seed;
        public readonly string FounderName;
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
            // strip out non-alphabetic, non-space
            var nsb = new StringBuilder(name.Length);
            foreach (var c in name)
                if (char.IsLetterOrDigit(c) || c == ' ')
                    nsb.Append(c);
            name = nsb.ToString();

            string lastTag = null;
            var outflow = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
                if (i == 0 || name[i - 1] == ' ')
                    outflow.Append(name[i]);
            foreach (var tag in SelectTagsFrom(outflow.ToString(), 0, 3))
                if (!MyAPIGateway.Session.Factions.FactionTagExists(lastTag = tag))
                    return tag;
            name = name.Replace(" ", "");
            foreach (var tag in SelectTagsFrom(StripVowels(name), 0, 3))
                if (!MyAPIGateway.Session.Factions.FactionTagExists(lastTag = tag))
                    return tag;
            foreach (var tag in SelectTagsFrom(name, 0, 3))
                if (!MyAPIGateway.Session.Factions.FactionTagExists(lastTag = tag))
                    return tag;
            if (lastTag != null)
                return lastTag;
            throw new Exception("Unable to find a tag for " + name);
        }

        public MyProceduralFactionSeed(string founderName, ulong seed)
        {
            Seed = seed;
            var random = new Random((int)seed);

            HueRotation = (float)random.NextDouble();
            SaturationModifier = MyMath.Clamp((float)random.NextNormal(), -1, 1);
            ValueModifier = MyMath.Clamp((float)random.NextNormal(), -1, 1);
            FounderName = founderName.Substring(0, 1).ToUpper() + founderName.Substring(1).ToLower();

            m_attributeWeight = new Dictionary<MyProceduralFactionSpeciality, float>();

            var bestScore = 0.0f;
            var bestKey = MyProceduralFactionSpeciality.Housing;
            foreach (var key in MyProceduralFactionSpeciality.Values)
            {
                var score = m_attributeWeight[key] = MyMath.Clamp((float)random.NextNormal(0.5, 0.5), 0, 2);
                if (!(score > bestScore)) continue;
                bestKey = key;
                bestScore = score;
            }
            BestSpeciality = bestKey;

            // Generate Name:
            // [Name]'s [Adjective] [Speciality] [Suffix]
            var name = new StringBuilder();
            name.Append(FounderName);
            if (random.NextDouble() > 0.2)
            {
                name.Append("'s");
                if (random.NextDouble() > 0.5)
                    name.Append(" ").Append(random.NextUniformChoice(Adjectives));
                name.Append(" ").Append(random.NextUniformChoice(BestSpeciality.FactionTags));

                if (random.NextDouble() > 0.75)
                    name.Append(" ").Append(random.NextUniformChoice(FactionSuffixes));
            }
            else
                name.Append(" ").Append(random.NextUniformChoice(FactionSuffixes));
            Name = name.ToString();
            Tag = SelectTag(Name).ToUpper();
        }

        // 0 to 1
        public readonly float HueRotation;
        // -1 to 1
        public readonly float SaturationModifier;
        // -1 to 1
        public readonly float ValueModifier;

        // General faction attributes
        private readonly Dictionary<MyProceduralFactionSpeciality, float> m_attributeWeight;
        public MyProceduralFactionSpeciality BestSpeciality { get; private set; }
        public IEnumerable<KeyValuePair<MyProceduralFactionSpeciality, float>> Specialities => m_attributeWeight;

        public float AttributeWeight(MyProceduralFactionSpeciality x)
        {
            return m_attributeWeight.GetValueOrDefault(x);
        }


        private bool m_creationFailed = false;
        private IMyFaction m_faction = null;
        // This needs to be on the main thread maybe?  Or at least threadsafe.  TODO
        public IMyFaction GetOrCreateFaction()
        {
            if (m_faction != null) return m_faction;
            if (m_creationFailed) return null;
            MyParallelUtilities.InvokeOnGameThreadBlocking(() =>
            {
                m_faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(Tag);
                if (m_faction != null) return;
                // Now we must create.
                var founderID = MyAPIGateway.Session.Player?.IdentityId ?? 0;
                var totalSpeciality = m_attributeWeight.Values.Sum();
                var avgSpeciality = totalSpeciality / m_attributeWeight.Count;
                var specializationString = new StringBuilder();
                specializationString.Append("We also specialize in ");
                var specials = m_attributeWeight.Where(x => x.Value > avgSpeciality).OrderByDescending(x => x.Value).Skip(1).ToList();
                for (var i = 0; i < specials.Count; i++)
                {
                    if (i > 0 && specials.Count > 2)
                        specializationString.Append(", ");
                    if (i > 0 && i == specials.Count - 1)
                    {
                        if (specials.Count <= 2) specializationString.Append(" ");
                        specializationString.Append("and ");
                    }
                    specializationString.Append(specials[i].Key.Description);
                }
                specializationString.Append(".");
//                SessionCore.Log("Making faction Tag={0}, Name={1}", Tag, Name);
                MyAPIGateway.Session.Factions.CreateFaction(founderID, Tag, Name, "Your place for " + BestSpeciality.Description + ".  " + specializationString, "");
                m_faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(Tag);
                if (m_faction == null)
                {
//                    SessionCore.Log("Failed to create faction Tag={0}, Name={1}", Tag, Name);
                    m_creationFailed = true;
                }
            });
            return m_faction;
        }

        public long GetFounder()
        {
            return GetOrCreateFaction().FounderId;
        }
        
        public override string ToString()
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("MyProceduralFactionSeed[");
            builder.Append("\tSeed=").Append(Seed).AppendLine();
            builder.Append("\tFounderName=\"").Append(FounderName).AppendLine("\"");
            builder.Append("\tName=\"").Append(Name).AppendLine("\"");
            builder.Append("\tTag=\"").Append(Tag).AppendLine("\"");
            builder.Append("\tHSVDelta=[").Append(HueRotation).Append(", ").Append(SaturationModifier).Append(", ").Append(ValueModifier).AppendLine("]");
            builder.Append("]");
            return builder.ToString();
        }
    }
}
