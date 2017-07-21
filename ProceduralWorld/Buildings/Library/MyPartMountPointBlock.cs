using System;
using System.Collections.Generic;
using Equinox.Utils;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    // Supported Mount Point Options
    // Name Format: "Dummy Type:Piece Name arguments..."
    public class MyPartMountPointBlock
    {
        public readonly MyPartMount Owner;
        public string Piece { private set; get; }
        public Base6Directions.Direction MountDirection6 { private set; get; }
        /// <summary>
        /// Location of the merge block.
        /// </summary>
        public Vector3I AnchorLocation { private set; get; }

        // This means _nothing_.  Don't use it except when computing the adjaceny rule of a mount point.
        internal MyAdjacencyRule AdjacencyRule { private set; get; }

        public MyPartMountPointBlock(MyPartMount owner)
        {
            Owner = owner;
        }

        public void Init(MyObjectBuilder_CubeBlock block, string piece, IEnumerable<string> args)
        {
            var dir6 = Base6Directions.GetOppositeDirection(Base6Directions.GetCross(block.BlockOrientation.Up, block.BlockOrientation.Forward));
            var anchorLoc = block.Min;
            var adjacencyRule = MyAdjacencyRule.Any;
            foreach (var arg in args)
            {
                if (arg.StartsWithICase("D:")) // Mount direction rule
                {
                    Base6Directions.Direction tmpMountDirection;
                    if (Enum.TryParse(arg.Substring(2), out tmpMountDirection))
                        dir6 = new MatrixI(block.BlockOrientation).GetDirection(tmpMountDirection);
                    else
                        SessionCore.Log("Failed to parse mount point direction argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase("A:")) // Anchor position rule
                {
                    Vector3I anchor;
                    if (MyPartDummyUtils.TryParseVector(arg.Substring(2), out anchor))
                    {
                        anchorLoc = block.Min + anchor;
                        continue;
                    }
                    SessionCore.Log("Failed to parse anchor location argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase("AR:")) // Adjacency Rule
                {
                    MyAdjacencyRule rule;
                    if (Enum.TryParse(arg.Substring(3), out rule))
                        adjacencyRule = rule;
                    else
                        SessionCore.Log("Failed to parse adjacency rule argument \"{0}\"", arg);
                }
                else
                    SessionCore.Log("Failed to parse mount point argument \"{0}\"", arg);
            }

            Piece = piece;
            MountDirection6 = dir6;
            AnchorLocation = anchorLoc;
            AdjacencyRule = adjacencyRule;
        }

        public void Init(MyObjectBuilder_PartMountPointBlock block)
        {
            Piece = block.Piece;
            MountDirection6 = block.MountDirection6;
            AnchorLocation = block.AnchorLocation;
        }

        public MyObjectBuilder_PartMountPointBlock GetObjectBuilder()
        {
            var res = new MyObjectBuilder_PartMountPointBlock
            {
                MountDirection6 = MountDirection6,
                Piece = Piece,
                AnchorLocation = AnchorLocation
            };
            return res;
        }

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        /// <summary>
        /// Location of the opposing merge block.
        /// </summary>
        public Vector3I MountLocation => AnchorLocation + MountDirection;

        public void GetTransforms(MyPartMountPointBlock other, HashSet<MatrixI> cache)
        {
            var dirSelf = Base6Directions.GetOppositeDirection(MountDirection6);
            var dirOther = other.MountDirection6;

            // Perp. axis using +2
            // Base direction for axis (first entry) using ~1
            var dirSelfI = ((int)dirSelf & ~1) + 2;
            var dirOtherI = ((int)dirOther & ~1) + 2;

            for (var i = 0; i < 4; i++)
            {
                var tmp = new MatrixI();
                tmp.SetDirection(dirOther, dirSelf);
                // Align one of the 4 perp. vectors
                var off = Base6Directions.EnumDirections[(dirOtherI + i) % 6];
                // With another perp vector
                var offTo = Base6Directions.EnumDirections[dirSelfI % 6];
                tmp.SetDirection(off, offTo);
                // Compute last direction
                tmp.SetDirection(Base6Directions.GetCross(dirOther, off), Base6Directions.GetCross(dirSelf, offTo));
                // And the translation
                tmp.Translation = AnchorLocation - Vector3I.TransformNormal(other.MountLocation, ref tmp);
                cache.Add(tmp);
            }
        }
    }
}