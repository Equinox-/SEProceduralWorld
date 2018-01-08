using System.ComponentModel;
using System.Xml;
using ProtoBuf;
using VRage.ObjectBuilders;
using VRageMath;
using System.Xml.Serialization;
using VRage;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    public class Ob_ProceduralConstruction
    {
        [ProtoMember]
        [XmlElement("Room")]
        public Ob_ProceduralRoom[] Rooms;
    }

    public class Ob_ProceduralRoom
    {
        [ProtoMember]
        public SerializableDefinitionId PrefabID;
        [ProtoMember]

        [XmlIgnore]
        public MatrixI Transform = new MatrixI(Vector3I.Zero, Base6Directions.Direction.Forward, Base6Directions.Direction.Up);

        [ProtoMember]
        public SerializableVector3I Position { get { return Transform.Translation; } set { Transform.Translation = value; } }

        [ProtoMember]
        public Base6Directions.Direction Forward
        {
            get { return Transform.Forward; }
            set
            {
                Transform.Forward = value;
                Transform.Right = Base6Directions.GetCross(Transform.Forward, Transform.Up);
            }
        }

        [ProtoMember]
        public Base6Directions.Direction Up
        {
            get { return Transform.Up; }
            set
            {
                Transform.Up = value;
                Transform.Right = Base6Directions.GetCross(Transform.Forward, Transform.Up);
            }
        }
    }
}
