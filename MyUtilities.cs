using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;
using Sandbox.Definitions;

namespace ProcBuild
{
    class MyUtilities
    {
//        public static Base6Directions.Direction TransformNormal(Base6Directions.Direction dir, MatrixI mat)
//        {
//            var ouf = Base6Directions.GetClosestDirection(Vector3I.TransformNormal(Base6Directions.GetIntVector(dir), ref mat));
//            return ouf;
//        }
        
        public static BoundingBox TransformBoundingBox(BoundingBoxI box, MatrixI matrix)
        {
            var res = new BoundingBox(Vector3I.Transform(box.Min, matrix), Vector3I.Transform(box.Max, matrix));
            res.Include(Vector3I.Transform(new Vector3I(box.Min.X, box.Min.Y, box.Max.Z), matrix));
            res.Include(Vector3I.Transform(new Vector3I(box.Min.X, box.Max.Y, box.Min.Z), matrix));
            res.Include(Vector3I.Transform(new Vector3I(box.Min.X, box.Max.Y, box.Max.Z), matrix));

            res.Include(Vector3I.Transform(new Vector3I(box.Max.X, box.Min.Y, box.Min.Z), matrix));
            res.Include(Vector3I.Transform(new Vector3I(box.Max.X, box.Min.Y, box.Max.Z), matrix));
            res.Include(Vector3I.Transform(new Vector3I(box.Max.X, box.Max.Y, box.Min.Z), matrix));
            return res;
        }

        public static IEnumerable<Vector3I> GetPositions(BoundingBoxI box)
        {
            for (var x = box.Min.X; x <= box.Max.X; x++)
                for (var y = box.Min.Y; y <= box.Max.Y; y++)
                    for (var z = box.Min.Z; z <= box.Max.Z; z++)
                        yield return new Vector3I(x, y, z);
        }
        public static IEnumerable<Vector3I> GetPositions(BoundingBox box)
        {
            for (var x = box.Min.X; x <= box.Max.X; x++)
                for (var y = box.Min.Y; y <= box.Max.Y; y++)
                    for (var z = box.Min.Z; z <= box.Max.Z; z++)
                        yield return new Vector3I(x, y, z);
        }

        internal static MatrixI Multiply(MatrixI right, MatrixI left)
        {
            MatrixI result;
            result.Backward = left.GetDirection(right.Backward);
            result.Right = left.GetDirection(right.Right);
            result.Up = left.GetDirection(right.Up);
            result.Translation = Vector3I.Transform(right.Translation, left);
            return result;
        }
    }
}
