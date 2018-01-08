using System;
using System.Collections.Generic;
using Equinox.Utils;
using Equinox.Utils.Logging;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    // Supported Mount Point Options
    // Name Format: "Dummy Type:Piece Name arguments..."
    public class PartMountPointBlock
    {
        public readonly PartMount Owner;
        public string Piece { private set; get; }
        public Base6Directions.Direction MountDirection6 { private set; get; }
        public Base6Directions.Direction? BiasDirection6 { private set; get; }
        public Base6Directions.Direction? SecondBiasDirection6 { private set; get; }
        /// <summary>
        /// Location of the merge block.
        /// </summary>
        public Vector3I AnchorLocation { private set; get; }

        // This means _nothing_.  Don't use it except when computing the adjaceny rule of a mount point.
        internal AdjacencyRule AdjacencyRule { private set; get; }

        private ILogging Logging => Owner.Owner.Manager;

        public PartMountPointBlock(PartMount owner)
        {
            Owner = owner;
        }

        public void Init(MyObjectBuilder_CubeBlock block, string piece, IEnumerable<string> args)
        {
            Piece = piece;
            MountDirection6 = Base6Directions.GetOppositeDirection(Base6Directions.GetCross(block.BlockOrientation.Up, block.BlockOrientation.Forward));
            AnchorLocation = block.Min;
            AdjacencyRule = AdjacencyRule.Any;
            BiasDirection6 = null;
            SecondBiasDirection6 = null;

            var blockOrientation = new MatrixI(block.BlockOrientation);
            foreach (var arg in args)
            {
                if (arg.StartsWithICase(PartDummyUtils.ArgumentMountDirection))
                {
                    Base6Directions.Direction tmpMountDirection;
                    if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentMountDirection.Length), out tmpMountDirection))
                        MountDirection6 = blockOrientation.GetDirection(tmpMountDirection);
                    else
                        Logging.Error("Failed to parse mount point direction argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase(PartDummyUtils.ArgumentBiasDirection))
                {
                    Base6Directions.Direction tmpBiasDirection;
                    if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentBiasDirection.Length), out tmpBiasDirection))
                        BiasDirection6 = blockOrientation.GetDirection(tmpBiasDirection);
                    else
                        Logging.Error("Failed to parse bias direction argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase(PartDummyUtils.ArgumentSecondBiasDirection))
                {
                    Base6Directions.Direction tmpBiasDirection;
                    if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentSecondBiasDirection.Length), out tmpBiasDirection))
                        SecondBiasDirection6 = blockOrientation.GetDirection(tmpBiasDirection);
                    else
                        Logging.Error("Failed to parse second bias direction argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase(PartDummyUtils.ArgumentAnchorPoint))
                {
                    Vector3I anchor;
                    if (PartDummyUtils.TryParseVector(arg.Substring(PartDummyUtils.ArgumentAnchorPoint.Length), out anchor))
                    {
                        AnchorLocation = block.Min + anchor;
                        continue;
                    }
                    Logging.Error("Failed to parse anchor location argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase(PartDummyUtils.ArgumentAdjacencyRule)) // Adjacency Rule
                {
                    AdjacencyRule rule;
                    if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentAdjacencyRule.Length), out rule))
                        AdjacencyRule = rule;
                    else
                        Logging.Error("Failed to parse adjacency rule argument \"{0}\"", arg);
                }
                else
                    Logging.Error("Failed to parse mount point argument \"{0}\"", arg);
            }
            // ReSharper disable once InvertIf
            if (SecondBiasDirection6.HasValue && !BiasDirection6.HasValue)
            {
                BiasDirection6 = SecondBiasDirection6;
                SecondBiasDirection6 = null;
            }
        }

        public void Init(Ob_Part.MountPoint.Block block)
        {
            Piece = block.Piece;
            MountDirection6 = block.MountDirection6;
            AnchorLocation = block.AnchorLocation;
            BiasDirection6 = block.BiasDirection6;
            SecondBiasDirection6 = block.SecondBiasDirection6;
            // ReSharper disable once InvertIf
            if (SecondBiasDirection6.HasValue && !BiasDirection6.HasValue)
            {
                BiasDirection6 = SecondBiasDirection6;
                SecondBiasDirection6 = null;
            }
        }

        public Ob_Part.MountPoint.Block GetObjectBuilder()
        {
            var res = new Ob_Part.MountPoint.Block
            {
                MountDirection6 = MountDirection6,
                Piece = Piece,
                AnchorLocation = AnchorLocation,
                BiasDirection6 = BiasDirection6,
                SecondBiasDirection6 = SecondBiasDirection6
            };
            return res;
        }

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        /// <summary>
        /// Location of the opposing merge block.
        /// </summary>
        public Vector3I MountLocation => AnchorLocation + MountDirection;

        public void GetTransforms(PartMountPointBlock other, HashSet<MatrixI> cache)
        {
            var dirSelf = Base6Directions.GetOppositeDirection(MountDirection6);
            var dirOther = other.MountDirection6;
            if (other.BiasDirection6.HasValue && BiasDirection6.HasValue)
            {
                // Simple case.  Only one possible transform.
                var tmp = new MatrixI();
                // Mount directions need to be aligned
                tmp.SetDirection(dirOther, dirSelf);
                // Bias directions must be aligned
                var biasSelf = BiasDirection6.Value;
                var biasOther = other.BiasDirection6.Value;
                tmp.SetDirection(biasOther, biasSelf);
                // Final alignment
                tmp.SetDirection(Base6Directions.GetCross(dirOther, biasOther), Base6Directions.GetCross(dirSelf, biasSelf));
                // Check secondary alignment when present.  If it fails just return.  These will never work.
                if (other.SecondBiasDirection6.HasValue && SecondBiasDirection6.HasValue &&
                    tmp.GetDirection(other.SecondBiasDirection6.Value) != SecondBiasDirection6.Value)
                    return;
                tmp.Translation = AnchorLocation - Vector3I.TransformNormal(other.MountLocation, ref tmp);
                cache.Add(tmp);
                return;

            }

            // Complicated case.  4 possibilities
            // Perp. axis using +2
            // Base direction for axis (first entry) using ~1
            var dirSelfI = ((int)dirSelf & ~1) + 2;
            var dirOtherI = ((int)dirOther & ~1) + 2;

            for (var i = 0; i < 4; i++)
            {
                var tmp = new MatrixI();
                tmp.SetDirection(dirOther, dirSelf);
                // Align one of the 4 perp. vectors with another perp vector
                var biasSelf = Base6Directions.EnumDirections[dirSelfI % 6];
                var biasOther = Base6Directions.EnumDirections[(dirOtherI + i) % 6];
                tmp.SetDirection(biasOther, biasSelf);
                // Complete the matrix
                tmp.SetDirection(Base6Directions.GetCross(dirOther, biasOther), Base6Directions.GetCross(dirSelf, biasSelf));
                tmp.Translation = AnchorLocation - Vector3I.TransformNormal(other.MountLocation, ref tmp);
                cache.Add(tmp);
            }
        }
        public bool TypeEquals(PartMountPointBlock other)
        {
            return Piece.Equals(other.Piece) && Owner.MountType.Equals(other.Owner.MountType);
        }
    }
}