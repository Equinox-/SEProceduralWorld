using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using VRage;
using VRage.ObjectBuilders;
using VRageMath;

namespace ProcBuild.Construction
{
    public class MyObjectBuilder_ProceduralConstruction
    {
        [ProtoMember]
        public MyObjectBuilder_ProceduralRoom[] Room;
    }

    public class MyObjectBuilder_ProceduralRoom
    {
        [ProtoMember, DefaultValue(0)]
        public long RoomID;
        [ProtoMember]
        public SerializableDefinitionId PrefabID;
        [ProtoMember]
        public float Priority;
        [ProtoMember]
        public MatrixI Transform;
        [ProtoMember]
        public MyObjectBuilder_ProceduralMountPoint[] MountPoints;
    }

    public class MyObjectBuilder_ProceduralMountPoint
    {
        [ProtoMember]
        public string TypeID;
        [ProtoMember]
        public string InstanceID;
        [ProtoMember]
        public long? OtherRoomID;
    }
}
