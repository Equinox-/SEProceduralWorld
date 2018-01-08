using System;
using System.ComponentModel;
using System.Xml.Serialization;
using Equinox.Utils;
using ProtoBuf;
using VRage;
using VRage.ObjectBuilders;
using VRageMath;

// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Equinox.ProceduralWorld.Buildings.Library
{
    [Serializable, ProtoContract]
    public class Ob_Part
    {
        public const int PartBuilderVersion = 1;

        [ProtoMember]
        public int BuilderVersion = 0;

        [ProtoMember]
        [XmlArrayItem("MyReservedSpace")]
        public ReservedSpace[] ReservedSpaces;

        [ProtoMember]
        [XmlArrayItem("MountPoint")]
        public MountPoint[] MountPoints;

        [ProtoMember]
        public SerializableVector3I[] OccupiedLocations;

        [ProtoMember]
        [XmlArrayItem("Count")]
        public SerializableTuple<SerializableDefinitionId, int>[] ComponentCost;

        [ProtoMember]
        [XmlArrayItem("Count")]
        public SerializableTuple<SerializableDefinitionId, int>[] BlockCountByType;

        [ProtoMember]
        [XmlArrayItem("PowerGroup")]
        public SerializableTuple<string, float>[] PowerConsumptionByGroup;

        public long ComputeHash()
        {
            var hash = 0L;
            foreach (var a in ReservedSpaces)
                hash ^= a.ComputeHash() * 233L;
            foreach (var a in MountPoints)
                hash ^= a.ComputeHash() * 307L;
            if (OccupiedLocations != null)
                foreach (var a in OccupiedLocations)
                    hash ^= a.GetHashCode() * 2473L;
            if (ComponentCost != null)
                foreach (var kv in ComponentCost)
                    hash ^= kv.Item1.GetHashCode() * 2099L * kv.Item2.GetHashCode();
            if (BlockCountByType != null)
                foreach (var kv in BlockCountByType)
                    hash ^= kv.Item1.GetHashCode() * 2099L * kv.Item2.GetHashCode();
            if (PowerConsumptionByGroup != null)
                foreach (var kv in PowerConsumptionByGroup)
                    hash ^= kv.Item1.GetHashCode() * 65651L * kv.Item2.GetHashCode();
            return hash;
        }



        [Serializable, ProtoContract]
        public class MountPoint
        {
            [ProtoMember]
            public string Name;
            [ProtoMember]
            public string Type;
            [ProtoMember]
            [XmlArrayItem("Block")]
            public Block[] Blocks;
            [ProtoMember, DefaultValue(AdjacencyRule.Any)]
            public AdjacencyRule AdjacencyRule = AdjacencyRule.Any;

            public long ComputeHash()
            {
                var hash = 0L;
                if (Name != null)
                    hash ^= Name.GetHashCode() * 12007L;
                if (Type != null)
                    hash ^= Type.GetHashCode() * 23071L;
                if (Blocks != null)
                    foreach (var block in Blocks)
                        hash ^= 563 * block.ComputeHash();
                hash ^= (long)AdjacencyRule * 93169L;
                return hash;
            }


            [Serializable, ProtoContract]
            public class Block
            {
                [ProtoMember]
                public string Piece;
                [ProtoMember]
                public Base6Directions.Direction MountDirection6;
                [ProtoMember]
                public SerializableVector3I AnchorLocation;
                [ProtoMember]
                public Base6Directions.Direction? BiasDirection6;
                [ProtoMember]
                public Base6Directions.Direction? SecondBiasDirection6;

                public long ComputeHash()
                {
                    var hash = 0L;
                    if (Piece != null)
                        hash ^= Piece.GetHashCode() * 102107L;
                    hash ^= (long)MountDirection6 * 85247L;
                    hash ^= (long)AnchorLocation.GetHashCode() * 7481L;
                    if (BiasDirection6.HasValue)
                        hash ^= (long)BiasDirection6.Value * 86531L;
                    else
                        hash *= 31L;
                    if (SecondBiasDirection6.HasValue)
                        hash ^= (long)SecondBiasDirection6.Value * 8652331L;
                    else
                        hash *= 9431L;
                    return hash;
                }
            }

        }

        [Serializable, ProtoContract]
        public class ReservedSpace
        {
            [ProtoMember]
            public SerializableVector3 Min;
            [ProtoMember]
            public SerializableVector3 Max;
            [ProtoMember]
            public bool IsShared;
            [ProtoMember]
            public bool IsOptional;

            public long ComputeHash()
            {
                return (Min.GetHashCode() * 67L) ^ (Max.GetHashCode() * 7481L) ^ (IsShared.GetHashCode() * 7) ^ (IsOptional.GetHashCode() * 5);
            }
        }
    }
}
