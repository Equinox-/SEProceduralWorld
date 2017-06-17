using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using ProtoBuf;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;

namespace Equinox.ProceduralWorld.Voxels
{
    [ProtoContract]
    public class MyAsteroidLayer
    {
        [ProtoMember]
        public double AsteroidMinSize;
        [ProtoMember]
        public double AsteroidMaxSize;

        [ProtoMember]
        public double AsteroidSpacing;
        [ProtoMember]
        public double AsteroidDensity;

        [ProtoMember]
        [DefaultValue(1)]
        public double UsableRegion = 1;

        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> RequiresOre = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> ProhibitsOre = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        [ProtoMember]
        [XmlArrayItem("Ore")]
        public SerializableDefinitionId[] RequiresOreSerial
        {
            get { return RequiresOre.Cast<SerializableDefinitionId>().ToArray(); }
            set
            {
                RequiresOre.Clear();
                foreach (var x in value)
                    RequiresOre.Add(x);
            }
        }

        [ProtoMember]
        [XmlArrayItem("Ore")]
        public SerializableDefinitionId[] ProhibitsOreSerial
        {
            get { return ProhibitsOre.Cast<SerializableDefinitionId>().ToArray(); }
            set
            {
                ProhibitsOre.Clear();
                foreach (var x in value)
                    ProhibitsOre.Add(x);
            }
        }
    }

    [ProtoContract]
    public class MyObjectBuilder_AsteroidRing
    {
        [ProtoMember]
        public float InnerRadius;

        [ProtoMember]
        public float OuterRadius;

        [ProtoMember]
        [DefaultValue(1)]
        public float VerticalScaleMult = 1;
    }

    [ProtoContract]
    public class MyObjectBuilder_AsteroidSphere
    {
        [ProtoMember]
        public float InnerRadius;

        [ProtoMember]
        public float OuterRadius;
    }

    [ProtoContract]
    public class MyObjectBuilder_AsteroidField
    {
        [ProtoMember]
        [DefaultValue(null)]
        public MyAsteroidLayer[] Layers;

        [ProtoMember]
        public MyPositionAndOrientation Transform;

        [ProtoMember]
        public int Seed;

        [ProtoMember]
        public double DensityRegionSize = .1;

        [ProtoMember]
        [DefaultValue(null)]
        public MyObjectBuilder_AsteroidSphere ShapeSphere;

        [ProtoMember]
        [DefaultValue(null)]
        public MyObjectBuilder_AsteroidRing ShapeRing;
    }
}
