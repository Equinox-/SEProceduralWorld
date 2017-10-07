using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.Utils.Session;
using VRageMath;

namespace Equinox.ProceduralWorld.Names
{
    public abstract class MyNameGeneratorBase : MyLoggingSessionComponent
    {
        public static readonly Type[] SuppliedDeps = { typeof(MyNameGeneratorBase) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        public abstract string Generate(ulong seed);
    }

    public abstract class MyObjectBuilder_NameGeneratorBase : MyObjectBuilder_ModSessionComponent
    {
    }
}
