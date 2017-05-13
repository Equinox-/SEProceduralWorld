﻿using System.ComponentModel;
using ProtoBuf;
using VRage.ObjectBuilders;
using VRageMath;

namespace ProcBuild.Storage
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