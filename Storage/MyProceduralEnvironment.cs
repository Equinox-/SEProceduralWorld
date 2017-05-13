using ProcBuild.Utils.Noise;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Storage
{
    public class MyProceduralEnvironment
    {
        public Vector3D Position;

        public MyProceduralEnvironment(Vector3D v)
        {
            Position = v;
        }

        public float OreConcentrationHere(MyDefinitionId oreID)
        {
            var hashCode = oreID.SubtypeName.GetHashCode();
            var localPos = (Vector3)(Position / 1024);
            localPos.X += (hashCode & 0xFF) * 1.0458F;
            localPos.Y += ((hashCode >> 8) & 0xFF) * 0.9275F;
            localPos.Z += ((hashCode >> 16) & 0xFF) * 1.1985F;
            var res = OctaveNoise.Generate(localPos.X, localPos.Y, localPos.Z, 8);
            return MyMath.Clamp(res, 0, 1);
        }
    }
}