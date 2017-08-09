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
    public class MyCompositeNameGenerator : MyNameGeneratorBase
    {
        private readonly List<KeyValuePair<MyNameGeneratorBase, float>> m_generators = new List<KeyValuePair<MyNameGeneratorBase, float>>();

        public override string Generate(ulong seed)
        {
            if (m_generators == null || m_generators.Count == 0) return "{no generators}";
            var nf = (seed * 9187UL) / ulong.MaxValue;
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

        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent configOriginal)
        {
            var config = configOriginal as MyObjectBuilder_CompositeNameGenerator;
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
                var nameGenDef = (MyObjectBuilder_ModSessionComponent)k.Generator;
                var component = MyModSessionComponentRegistry.Get(nameGenDef).Activator();
                component.LoadConfiguration(nameGenDef);
                m_generators.Add(new KeyValuePair<MyNameGeneratorBase, float>((MyNameGeneratorBase)component,
                    cs));
            }
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            var result = new MyObjectBuilder_CompositeNameGenerator();
            float cf = 0;
            foreach (var kv in m_generators)
            {
                result.Generators.Add(
                    new MyObjectBuilder_CompositeNameGeneratorEntry()
                    {
                        Generator = (MyObjectBuilder_NameGeneratorBase)kv.Key.SaveConfiguration(),
                        Weight = kv.Value - cf
                    });
                cf += kv.Value;
            }
            return result;
        }
    }

    public class MyObjectBuilder_CompositeNameGenerator : MyObjectBuilder_NameGeneratorBase
    {
        [XmlElement("Generator")]
        public List<MyObjectBuilder_CompositeNameGeneratorEntry> Generators = new List<MyObjectBuilder_CompositeNameGeneratorEntry>();
    }

    public class MyObjectBuilder_CompositeNameGeneratorEntry
    {
        // This is only used so the automatic typing works.  I'd do xsi:type but it just doesn't work in mods.
        [XmlElement("Exotic", typeof(MyObjectBuilder_ExoticNameGenerator))]
        [XmlElement("Statistical", typeof(MyObjectBuilder_StatisticalNameGenerator))]
        public MyObjectBuilder_NameGeneratorBase[] Generators
        {
            get { return new[] {Generator}; }
            set { Generator = value != null && value.Length > 0 ? value[0] : null; }
        }

        [XmlIgnore] public MyObjectBuilder_NameGeneratorBase Generator;

        public float Weight = 1;
    }
}
