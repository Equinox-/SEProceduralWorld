using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// All credits go to Keen Software House
// https://github.com/KeenSoftwareHouse/SpaceEngineers/tree/master/Sources/VRage/Noise
namespace ProcBuild.Utils.Noise.VRage
{
    public interface IMyModule
    {
        double GetValue(double x);
        double GetValue(double x, double y);
        double GetValue(double x, double y, double z);
    }
}
