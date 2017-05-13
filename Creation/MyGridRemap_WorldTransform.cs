using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Creation
{
    public class MyGridRemap_WorldTransform : IMyGridRemap
    {
        public MatrixD WorldTransform { get; set; }
        public Vector3 WorldLinearVelocity { get; set; } = Vector3.Zero;

        private void ApplyTo(MyObjectBuilder_CubeGrid grid)
        {
            if (grid.PositionAndOrientation.HasValue)
                grid.PositionAndOrientation = new MyPositionAndOrientation(MatrixD.Multiply(grid.PositionAndOrientation.Value.GetMatrix(), WorldTransform));
            grid.AngularVelocity = Vector3.TransformNormal(grid.AngularVelocity, WorldTransform);
            grid.LinearVelocity = Vector3.TransformNormal(grid.LinearVelocity, WorldTransform);
            grid.LinearVelocity += WorldLinearVelocity;
        }

        public void Remap(MyObjectBuilder_CubeGrid grid)
        {
            ApplyTo(grid);

            foreach (var x in grid.CubeBlocks)
            {
                var proj = x as MyObjectBuilder_ProjectorBase;
                if (proj?.ProjectedGrid != null)
                    ApplyTo(proj.ProjectedGrid);
            }
        }

        public void Reset()
        {
        }
    }
}
