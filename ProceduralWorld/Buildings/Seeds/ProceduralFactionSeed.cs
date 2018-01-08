using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.Utils;
using Equinox.Utils.Random;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.VisualScripting;
using VRageMath;
using MyVisualScriptLogicProvider = Sandbox.Game.MyVisualScriptLogicProvider;

namespace Equinox.ProceduralWorld.Buildings.Seeds
{
    public class ProceduralFactionSeed
    {
        public static readonly string[] FactionSuffixes = { "Inc.", "LLC.", "Co.", "Itpl.", "Total", "United" };
        public static readonly string[] Adjectives = { "Dreamy", "Amazing", "World Famous", "General", };

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

        public ProceduralFactionSeed(Ob_ProceduralFaction ob)
        {
            Seed = ob.Seed;
            FounderName = ob.FounderName;
            Name = ob.Name;
            Tag = ob.Tag;
            HueRotation = ob.ColorModifier.Hue;
            SaturationModifier = ob.ColorModifier.Saturation;
            ValueModifier = ob.ColorModifier.Value;
            {
                var best = ProceduralFactionSpeciality.Mining;
                var bestWeight = 0f;
                m_attributeWeight = new Dictionary<ProceduralFactionSpeciality, float>();
                foreach (var speciality in ob.Specialities)
                {
                    m_attributeWeight[speciality.Speciality] = speciality.Weight;
                    if (speciality.Weight > bestWeight)
                    {
                        best = speciality.Speciality;
                        bestWeight = speciality.Weight;
                    }
                }
                BestSpeciality = best;
            }
        }

        public ProceduralFactionSeed(string founderName, ulong seed)
        {
            Seed = seed;
            var random = new Random((int)seed);

            HueRotation = (float)random.NextDouble();
            SaturationModifier = MyMath.Clamp((float)random.NextNormal(), -1, 1);
            ValueModifier = MyMath.Clamp((float)random.NextNormal(), -1, 1);
            FounderName = founderName.Substring(0, 1).ToUpper() + founderName.Substring(1).ToLower();

            m_attributeWeight = new Dictionary<ProceduralFactionSpeciality, float>();

            var bestScore = 0.0f;
            var bestKey = ProceduralFactionSpeciality.Housing;
            foreach (var key in ProceduralFactionSpeciality.Values)
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

        public Ob_ProceduralFaction GetObjectBuilder()
        {
            return new Ob_ProceduralFaction()
            {
                Seed = Seed,
                FounderName = FounderName,
                Name = Name,
                Tag = Tag,
                ColorModifier = new Ob_ProceduralFaction.HsvModifier()
                {
                    Hue = HueRotation,
                    Saturation = SaturationModifier,
                    Value = ValueModifier
                },
                Specialities = m_attributeWeight.Select(x =>
                    new Ob_ProceduralFaction.FactionSpeciality() { Speciality = x.Key, Weight = x.Value }).ToList()
            };
        }

        // 0 to 1
        public readonly float HueRotation;
        // -1 to 1
        public readonly float SaturationModifier;
        // -1 to 1
        public readonly float ValueModifier;

        // General faction attributes
        private readonly Dictionary<ProceduralFactionSpeciality, float> m_attributeWeight;
        public ProceduralFactionSpeciality BestSpeciality { get; private set; }
        public IEnumerable<KeyValuePair<ProceduralFactionSpeciality, float>> Specialities => m_attributeWeight;

        public float AttributeWeight(ProceduralFactionSpeciality x)
        {
            return m_attributeWeight.GetValueOrDefault(x);
        }


        private bool m_creationFailed = false;
        private IMyFaction m_faction = null;

        public IMyFaction GetOrCreateFaction()
        {
            if (m_faction != null) return m_faction;
            if (m_creationFailed) return null;
            ParallelUtilities.InvokeOnGameThreadBlocking(() =>
            {
                m_faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(Tag);
                if (m_faction != null) return;

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
                m_faction = CreateNpcFaction(Tag, Name, "Your place for " + BestSpeciality.Description + ".  " + specializationString, "");
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

        private static readonly HashSet<long> m_existingIds = new HashSet<long>();
        private static readonly List<IMyIdentity> m_identities = new List<IMyIdentity>();

        public static IMyFaction CreateNpcFaction(string tag, string name, string desc, string privateInfo)
        {
            var pirateIdentity = MyVisualScriptLogicProvider.GetPirateId();
            var pirateFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(pirateIdentity);
            m_existingIds.Clear();
            foreach (var member in pirateFaction.Members)
                m_existingIds.Add(member.Key);
            MyAPIGateway.Session.Factions.AddNewNPCToFaction(pirateFaction.FactionId);
            
            long npcId = 0;
            m_identities.Clear();
            MyAPIGateway.Players.GetAllIdentites(m_identities);
            foreach (var x in m_identities)
                if (!m_existingIds.Contains(x.IdentityId) &&
                    MyAPIGateway.Session.Factions.TryGetPlayerFaction(x.IdentityId)?.FactionId ==
                    pirateFaction.FactionId)
                {
                    npcId = x.IdentityId;
                    break;
                }
            if (npcId != 0)
            {
#pragma warning disable 618
                // I know it's obselete, but because AddPlayerToFactionInternal
                // (used by AddNewNPCToFaction) doesn't update Faction.Members we can't use KickMember
                MyAPIGateway.Session.Factions.KickPlayerFromFaction(npcId);
#pragma warning restore 618
                MyAPIGateway.Session.Factions.CreateFaction(npcId, tag, name, desc, privateInfo);
            }
            return MyAPIGateway.Session.Factions.TryGetFactionByTag(tag);
        }
    }

    public class Ob_ProceduralFaction
    {
        public ulong Seed = 0;
        public string Name = "Amazing Artificial Antelopes";
        public string Tag = "AAA";
        public string FounderName = "Antelope Andy";

        [XmlElement("Speciality")]
        public List<FactionSpeciality> Specialities = new List<FactionSpeciality>();

        public HsvModifier ColorModifier = new HsvModifier();

        public class FactionSpeciality
        {
            [XmlAttribute("Speciality")]
            public string SpecialitySerial
            {
                get { return Speciality?.Name; }
                set { Speciality = ProceduralFactionSpeciality.ByName(value); }
            }

            [XmlIgnore]
            public ProceduralFactionSpeciality Speciality;
            [XmlAttribute("Weight")]
            public float Weight;
        }

        public class HsvModifier
        {
            // 0-1
            [XmlAttribute("Hue")]
            public float Hue;

            // -1 to 1
            [XmlAttribute("Saturation")]
            public float Saturation;

            // -1 to 1
            [XmlAttribute("Value")]
            public float Value;
        }
    }
}
