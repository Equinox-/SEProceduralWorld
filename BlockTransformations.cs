using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace ProcBuild
{
    public class BlockTransformations
    {
        public static void ComputeBlockMax(ref MyObjectBuilder_CubeBlock block, out Vector3I max)
        {
            var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
            if (definition == null)
            {
                max = block.Min;
                return;
            }
            var size = definition.Size - 1;
            var localMatrix = new MatrixI(new MyBlockOrientation(block.BlockOrientation.Up, block.BlockOrientation.Forward));
            Vector3I.TransformNormal(ref size, ref localMatrix, out size);
            Vector3I.Abs(ref size, out size);
            max = block.Min + size;
        }

        public static MyObjectBuilder_CubeBlock CopyAndTransform(MyObjectBuilder_CubeBlock block, MatrixI transform)
        {
            var output = (MyObjectBuilder_CubeBlock)block.Clone();
            output.BlockOrientation.Forward = transform.GetDirection(block.BlockOrientation.Forward);
            output.BlockOrientation.Up = transform.GetDirection(block.BlockOrientation.Up);

            var cMin = (Vector3I)block.Min;
            Vector3I cMax;
            ComputeBlockMax(ref block, out cMax);
            
            Vector3I.Transform(ref cMin, ref transform, out cMin);
            Vector3I.Transform(ref cMax, ref transform, out cMax);

            output.Min = Vector3I.Min(cMin, cMax);

            return output;
        }
    }
}
