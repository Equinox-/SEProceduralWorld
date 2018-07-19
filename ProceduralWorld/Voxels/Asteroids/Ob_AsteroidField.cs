using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Equinox.Utils.Session;
using ProtoBuf;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    [ProtoContract]
    public class AsteroidLayer
    {
        [ProtoMember]
        [DefaultValue(false)]
        [XmlElement]
        public bool ExcludeInvalid = false;
        
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
        public readonly HashSet<string> RequiresOre = new HashSet<string>();

        [XmlIgnore]
        public readonly HashSet<string> ProhibitsOre = new HashSet<string>();

        [ProtoMember]
        [XmlArrayItem("Ore")]
        public string[] RequiresOreSerial
        {
            get { return RequiresOre.ToArray(); }
            set
            {
                RequiresOre.Clear();
                foreach (var x in value)
                    RequiresOre.Add(x);
            }
        }

        [ProtoMember]
        [XmlArrayItem("Ore")]
        public string[] ProhibitsOreSerial
        {
            get { return ProhibitsOre.ToArray(); }
            set
            {
                ProhibitsOre.Clear();
                foreach (var x in value)
                    ProhibitsOre.Add(x);
            }
        }
    }

    [ProtoContract]
    public class Ob_AsteroidRing
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
    public class Ob_AsteroidSphere
    {
        [ProtoMember]
        public float InnerRadius;

        [ProtoMember]
        public float OuterRadius;
    }

    [ProtoContract]
    public class Ob_AsteroidField : Ob_ModSessionComponent
    {
        [ProtoMember]
        [DefaultValue(null)]
        [XmlArrayItem("MyAsteroidLayer")]
        public AsteroidLayer[] Layers;

        [XmlIgnore]
        public MatrixD Transform = MatrixD.Identity;

        [ProtoMember]
        public SerializableVector3D Position { get { return Transform.Translation; } set { Transform.Translation = value; } }

        [ProtoMember]
        public SerializableVector3D Forward
        {
            get { return Transform.Forward; }
            set
            {
                Transform.Forward = value;
                Transform.Right = Vector3D.Cross(Transform.Forward, Transform.Up);
            }
        }

        [ProtoMember]
        public SerializableVector3D Up
        {
            get { return Transform.Up; }
            set
            {
                Transform.Up = value;
                Transform.Right = Vector3D.Cross(Transform.Forward, Transform.Up);
            }
        }
        
        [ProtoMember]
        public int Seed;

        [ProtoMember]
        [DefaultValue(null)]
        public Ob_AsteroidSphere ShapeSphere;

        [ProtoMember]
        [DefaultValue(null)]
        public Ob_AsteroidRing ShapeRing;
    }
}
