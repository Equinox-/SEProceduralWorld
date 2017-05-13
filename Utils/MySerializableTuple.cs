using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace ProcBuild.Utils
{
    [Serializable, ProtoContract]
    public struct MySerializableTuple<T1, T2>
    {
        [ProtoMember]
        public T1 Item1;
        [ProtoMember]
        public T2 Item2;
    }

    public static class MySerializableTuple
    {
        public static MySerializableTuple<T1, T2> Create<T1, T2>(T1 a, T2 b)
        {
            return new MySerializableTuple<T1, T2>() { Item1 = a, Item2 = b };
        }
    }
}
