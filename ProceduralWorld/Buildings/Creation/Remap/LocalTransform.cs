using System.Collections.Generic;
using Equinox.Utils;
using Equinox.Utils.Logging;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation.Remap
{
    public class GridRemap_LocalTransform : IGridRemap
    {
        public MatrixI LocalTransform { get; set; }

        public GridRemap_LocalTransform(ILoggingBase root) : base(root)
        {
        }

        public override void Remap(MyObjectBuilder_CubeGrid grid)
        {
            var transformCopy = LocalTransform;

            var minToMin = new Dictionary<Vector3I, Vector3I>(grid.CubeBlocks.Count * 3 / 2);
            foreach (var x in grid.CubeBlocks)
            {
                var orig = (Vector3I)x.Min;
                var cMin = orig;
                Vector3I cMax;
                BlockTransformations.ComputeBlockMax(x, out cMax);
                x.BlockOrientation.Forward = LocalTransform.GetDirection(x.BlockOrientation.Forward);
                x.BlockOrientation.Up = LocalTransform.GetDirection(x.BlockOrientation.Up);
                Vector3I.Transform(ref cMin, ref transformCopy, out cMin);
                Vector3I.Transform(ref cMax, ref transformCopy, out cMax);
                minToMin[orig] = x.Min = Vector3I.Min(cMin, cMax);

                var proj = x as MyObjectBuilder_ProjectorBase;
                // Don't have to update the rotation; it is bound to the world matrix of the projector.
                if (proj != null)
                    Vector3I.TransformNormal(ref proj.ProjectionOffset, ref transformCopy, out proj.ProjectionOffset);
            }

            if (grid.BlockGroups != null)
                foreach (var g in grid.BlockGroups)
                    for (var i = 0; i < g.Blocks.Count; i++)
                    {
                        Vector3I tmpOut;
                        if (minToMin.TryGetValue(g.Blocks[i], out tmpOut))
                            g.Blocks[i] = tmpOut;
                        else
                            g.Blocks[i] = Vector3I.MaxValue; // sorta discards it?
                    }

            if (grid.ConveyorLines != null)
                foreach (var l in grid.ConveyorLines)
                {
                    l.StartDirection = LocalTransform.GetDirection(l.StartDirection);
                    l.StartPosition = Vector3I.Transform(l.StartPosition, ref transformCopy);

                    l.EndDirection = LocalTransform.GetDirection(l.EndDirection);
                    l.EndPosition = Vector3I.Transform(l.EndPosition, ref transformCopy);

                    if (l.Sections == null) continue;
                    for (var s = 0; s < l.Sections.Count; s++)
                        l.Sections[s] = new SerializableLineSectionInformation() { Direction = LocalTransform.GetDirection(l.Sections[s].Direction), Length = l.Sections[s].Length };
                }
        }

        public override void Reset()
        {
        }
    }
}
