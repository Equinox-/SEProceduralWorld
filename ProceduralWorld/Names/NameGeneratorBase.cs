using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.Utils.Session;
using VRageMath;

namespace Equinox.ProceduralWorld.Names
{
    public abstract class NameGeneratorBase : LoggingSessionComponent
    {
        public static readonly Type[] SuppliedDeps = { typeof(NameGeneratorBase) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        public abstract string Generate(ulong seed);
    }

    public abstract class Ob_NameGeneratorBase : Ob_ModSessionComponent
    {
    }
}
