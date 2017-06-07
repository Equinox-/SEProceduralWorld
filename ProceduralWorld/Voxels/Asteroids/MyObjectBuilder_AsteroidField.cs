using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public double UsableRegion;

        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> RequiresOre = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> ProhibitsOre = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        [ProtoMember]
        [XmlElement("RequiredOres")]
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
        [XmlElement("ProhibitedOres")]
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
    public class MyObjectBuilder_AsteroidRing : MyObjectBuilder_AsteroidField
    {
        [ProtoMember]
        public double InnerRadius;

        [ProtoMember]
        public double OuterRadius;

        [ProtoMember]
        [DefaultValue(1)]
        public double VerticalScaleMult = 1;
    }

    [ProtoContract]
    public class MyObjectBuilder_AsteroidSphere : MyObjectBuilder_AsteroidField
    {
        [ProtoMember]
        public double InnerRadius;

        [ProtoMember]
        public double OuterRadius;
    }
    
    [ProtoContract]
    public abstract class MyObjectBuilder_AsteroidField
    {
        [ProtoMember]
        [DefaultValue(null)]
        public MyAsteroidLayer[] Layers;

        [ProtoMember]
        public MyPositionAndOrientation Transform;
    }
}
