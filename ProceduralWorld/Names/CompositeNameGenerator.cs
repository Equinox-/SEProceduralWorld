using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.Utils.Session;
using VRage.Utils;
using System.Xml.Serialization;

namespace Equinox.ProceduralWorld.Names
{
    public class CompositeNameGenerator : NameGeneratorBase
    {
        private readonly List<KeyValuePair<NameGeneratorBase, float>> m_generators = new List<KeyValuePair<NameGeneratorBase, float>>();

        public override string Generate(ulong seed)
        {
            if (m_generators == null || m_generators.Count == 0) return "{no generators}";
            var nf = (seed * (ulong.MaxValue - 9189123987UL)) / ulong.MaxValue;
            // binary search generator list.
            var left = 0;
            var right = m_generators.Count - 1;
            while (left < right)
            {
                var m = (left + right) / 2;
                if (m_generators[m].Value > nf)
                    right = m - 1;
                else
                    left = m + 1;
            }
            return m_generators[left].Key.Generate(seed);
        }

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_CompositeNameGenerator;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}",
                    configOriginal.GetType(),
                    GetType());
                return;
            }
            var total = 0f;
            foreach (var k in config.Generators)
                total += k.Weight;
            m_generators.Clear();
            var cs = 0f;
            foreach (var k in config.Generators)
            {
                cs += k.Weight / total;
                var nameGenDef = (Ob_ModSessionComponent)k.Generator;
                var component = ModSessionComponentRegistry.Get(nameGenDef).Activator();
                component.LoadConfiguration(nameGenDef);
                m_generators.Add(new KeyValuePair<NameGeneratorBase, float>((NameGeneratorBase)component,
                    cs));
            }
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            var result = new Ob_CompositeNameGenerator();
            float cf = 0;
            foreach (var kv in m_generators)
            {
                result.Generators.Add(
                    new Ob_CompositeNameGeneratorEntry()
                    {
                        Generator = (Ob_NameGeneratorBase)kv.Key.SaveConfiguration(),
                        Weight = kv.Value - cf
                    });
                cf += kv.Value;
            }
            return result;
        }
    }

    public class Ob_CompositeNameGenerator : Ob_NameGeneratorBase
    {
        [XmlElement("Generator")]
        public List<Ob_CompositeNameGeneratorEntry> Generators = new List<Ob_CompositeNameGeneratorEntry>();
    }

    public class Ob_CompositeNameGeneratorEntry
    {
        // This is only used so the automatic typing works.  I'd do xsi:type but it just doesn't work in mods.
        [XmlElement("Exotic", typeof(Ob_ExoticNameGenerator))]
        [XmlElement("Statistical", typeof(Ob_StatisticalNameGenerator))]
        public Ob_NameGeneratorBase[] Generators
        {
            get { return new[] {Generator}; }
            set { Generator = value != null && value.Length > 0 ? value[0] : null; }
        }

        [XmlIgnore] public Ob_NameGeneratorBase Generator;

        public float Weight = 1;
    }
}
