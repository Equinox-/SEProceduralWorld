using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Storage;

namespace ProcBuild.Generation
{
    public static class MyNameGenerator
    {
        public static string GetName(this MyProceduralRoom module)
        {
            return "M" + module.GetHashCode().ToString("X");
        }

        public static string GetName(this MyProceduralConstruction construction)
        {
            return "C" + construction.GetHashCode().ToString("X");
        }
    }
}
