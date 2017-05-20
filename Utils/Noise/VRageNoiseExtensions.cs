using ProcBuild.Utils.Noise.VRage;
using VRageMath;

namespace ProcBuild.Utils.Noise
{
    public static class VRageNoiseExtensions
    {
        public static double GetValue(this IMyModule module, Vector2 pos)
        {
            return module.GetValue(pos.X, pos.Y);
        }

        public static double GetValue(this IMyModule module, Vector2D pos)
        {
            return module.GetValue(pos.X, pos.Y);
        }

        public static double GetValue(this IMyModule module, Vector3 pos)
        {
            return module.GetValue(pos.X, pos.Y, pos.Z);
        }
        public static double GetValue(this IMyModule module, Vector3D pos)
        {
            return module.GetValue(pos.X, pos.Y, pos.Z);
        }
    }
}
