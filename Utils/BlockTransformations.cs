using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Utils
{
    public class BlockTransformations
    {
        public static void ComputeBlockMax(MyObjectBuilder_CubeBlock block, ref MyCubeBlockDefinition definition, out Vector3I max)
        {
            if (definition == null)
            {
                max = block.Min;
                return;
            }
            var size = definition.Size - 1;
            var localMatrix = new MatrixI(new MyBlockOrientation(block.BlockOrientation.Forward, block.BlockOrientation.Up));
            Vector3I.TransformNormal(ref size, ref localMatrix, out size);
            Vector3I.Abs(ref size, out size);
            max = block.Min + size;
        }

        public static void ComputeBlockMax(MyObjectBuilder_CubeBlock block, out Vector3I max)
        {
            var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
            ComputeBlockMax(block, ref definition, out max);
        }

        private static Dictionary<MyObjectBuilder_CubeGrid, MyObjectBuilder_CubeGrid> gridCache = new Dictionary<MyObjectBuilder_CubeGrid, MyObjectBuilder_CubeGrid>();
        public static MyObjectBuilder_CubeGrid CloneGrid(MyObjectBuilder_CubeGrid src)
        {
            return (MyObjectBuilder_CubeGrid)src.Clone();
//            MyObjectBuilder_CubeGrid val;
//            if (gridCache.TryGetValue(src, out val)) return val;
//            return gridCache[src] = (MyObjectBuilder_CubeGrid) src.Clone();
        }

        public static void ApplyTransformation(MyObjectBuilder_CubeBlock output, MatrixI transform)
        {
            var cMin = (Vector3I)output.Min;
            Vector3I cMax;
            ComputeBlockMax(output, out cMax);

            output.BlockOrientation.Forward = transform.GetDirection(output.BlockOrientation.Forward);
            output.BlockOrientation.Up = transform.GetDirection(output.BlockOrientation.Up);


            Vector3I.Transform(ref cMin, ref transform, out cMin);
            Vector3I.Transform(ref cMax, ref transform, out cMax);

            output.Min = Vector3I.Min(cMin, cMax);
        }
    }
}
