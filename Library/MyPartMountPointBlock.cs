using System;
using System.Collections.Generic;
using ProcBuild.Library;
using VRage.Game;
using VRageMath;

namespace ProcBuild
{
    // Supported Mount Point Options
    // Name Format: "Dummy Type:Piece Name arguments..."
    public class MyPartMountPointBlock
    {
        public readonly MyObjectBuilder_CubeBlock m_block;
        public MyPartMount Owner { private set; get; }
        public readonly string m_piece;
        private readonly Vector3I m_anchorOffset;

        // This means _nothing_.  Don't use it except when computing the adjaceny rule of a mount point.
        internal MyAdjacencyRule AdjacencyRule { private set; get; }

        public MyPartMountPointBlock(MyPartMount owner, string piece, MyObjectBuilder_CubeBlock block, IEnumerable<string> args)
        {
            Owner = owner;
            m_piece = piece;
            m_block = block;
            MountDirection6 = Base6Directions.GetOppositeDirection(Base6Directions.GetCross(m_block.BlockOrientation.Up, m_block.BlockOrientation.Forward));
            m_anchorOffset = Vector3I.Zero;
            AdjacencyRule = MyAdjacencyRule.Any;
            foreach (var arg in args)
            {
                if (arg.StartsWithICase("D:")) // Mount direction rule
                {
                    Base6Directions.Direction tmpMountDirection;
                    if (Enum.TryParse(arg.Substring(2), out tmpMountDirection))
                        MountDirection6 = new MatrixI(m_block.BlockOrientation).GetDirection(tmpMountDirection);
                    else
                        SessionCore.Instance.Logger.Log("Failed to parse mount point direction argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase("A:")) // Anchor position rule
                {
                    Vector3I anchor;
                    if (MyPartDummyUtils.TryParseVector(arg.Substring(2), out anchor))
                    {
                        m_anchorOffset = anchor;
                        continue;
                    }
                    SessionCore.Instance.Logger.Log("Failed to parse anchor location argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase("AR:")) // Adjacency Rule
                {
                    MyAdjacencyRule rule;
                    if (Enum.TryParse(arg.Substring(3), out rule))
                        AdjacencyRule = rule;
                    else
                        SessionCore.Log("Failed to parse adjacency rule argument \"{0}\"", arg);
                }
                else
                    SessionCore.Instance.Logger.Log("Failed to parse mount point argument \"{0}\"", arg);
            }
        }

        public Base6Directions.Direction MountDirection6 { private set; get; }

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        public Vector3I AnchorLocation => m_block.Min + m_anchorOffset;

        public Vector3I MountLocation => AnchorLocation + MountDirection;

        public void GetTransforms(MyPartMountPointBlock other, HashSet<MatrixI> cache)
        {
            var dirSelf = Base6Directions.GetOppositeDirection(MountDirection6);
            var dirOther = other.MountDirection6;
            var dirSelfI = ((int)dirSelf + 2) & ~1;
            var dirOtherI = ((int)dirOther + 2) & ~1;
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 2) % 4;
                var tmp = new MatrixI();
                tmp.SetDirection(dirOther, dirSelf);
                var off = Base6Directions.EnumDirections[(dirOtherI + i) % 6];
                var offTo = Base6Directions.EnumDirections[(dirSelfI + 0) % 6];
                tmp.SetDirection(off, offTo);
                tmp.SetDirection(Base6Directions.GetCross(dirOther, off), Base6Directions.GetCross(dirSelf, offTo));
                tmp.Translation = AnchorLocation - Vector3I.TransformNormal(other.MountLocation, ref tmp);
                cache.Add(tmp);
            }
        }
    }
}