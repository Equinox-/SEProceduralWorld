using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.Entities.Blocks;
using VRage.Game;
using VRageMath;
using VRageRender;

namespace ProcBuild
{
    public class PartMountPointBlock
    {
        public readonly MyObjectBuilder_CubeBlock m_block;
        public readonly string m_piece;

        public PartMountPointBlock(string piece, MyObjectBuilder_CubeBlock block)
        {
            m_piece = piece;
            m_block = block;
        }

        public Base6Directions.Direction MountDirection6 => Base6Directions.GetOppositeDirection(Base6Directions.GetCross(m_block.BlockOrientation.Up, m_block.BlockOrientation.Forward));

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        public Vector3I MountLocation => m_block.Min + MountDirection;

        public void GetTransforms(PartMountPointBlock other, ref Matrix[] cache)
        {
            if (cache.Length != 4) throw new ArgumentException("Cache size must be 4");
            var dirSelf = Base6Directions.GetOppositeDirection(MountDirection6);
            var dirOther = other.MountDirection6;
            var dirSelfI = ((int)dirSelf + 2) & ~1;
            var dirOtherI = ((int)dirOther + 2) & ~1;
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 2) % 4;
                cache[i].SetDirectionVector(dirOther, Base6Directions.GetVector(dirSelf));
                cache[i].SetDirectionVector(Base6Directions.EnumDirections[(dirOtherI + i) % 6], Base6Directions.Directions[(dirSelfI + 0) % 6]);
                cache[i].SetDirectionVector(Base6Directions.EnumDirections[(dirOtherI + j) % 6], Base6Directions.Directions[(dirSelfI + 2) % 6]);
                cache[i].Translation = Vector3.TransformNormal(other.MountLocation, ref cache[i]) - (Vector3I) m_block.Min;
            }
        }
    }

    public class PartMount
    {
        public readonly string m_mountType;
        public readonly string m_mountName;
        public readonly SortedDictionary<string, List<PartMountPointBlock>> m_blocks;
        private readonly Part m_part;

        public PartMount(Part part, string mountType, string mountName)
        {
            m_part = part;
            m_mountType = mountType;
            m_mountName = mountName;
            m_blocks = new SortedDictionary<string, List<PartMountPointBlock>>();
        }

        internal void Add(PartMountPointBlock block)
        {
            List<PartMountPointBlock> points;
            if (!m_blocks.TryGetValue(block.m_piece, out points))
                points = m_blocks[block.m_piece] = new List<PartMountPointBlock>(2);
            points.Add(block);
        }

        public HashSet<MatrixI> GetTransform(PartMount otherMount)
        {
            if (m_blocks.Count == 0) return null;
            var options = new HashSet<MatrixI>();
            var availableKeys = m_blocks.Keys.Union(otherMount.m_blocks.Keys);
            var init = false;
            foreach (var key in availableKeys)
            {
                foreach (var mine in m_blocks[key])
                {
                    foreach (var other in otherMount.m_blocks[key])
                    {
                        if (!init)
                        {
                            // Haven't init-d?  Calc transform
                        }
                    }
                }
                init = true;
            }
            return options.Count > 0 ? options : null;
        }
    }

    public class Part
    {
        private readonly MyPrefabDefinition m_prefab;
        private readonly MyObjectBuilder_CubeGrid m_grid;
        private readonly BoundingBox m_boundingBox;

        private readonly Dictionary<string, Dictionary<string, PartMount>> m_mountPoints;

        public Part(MyPrefabDefinition prefab)
        {
            m_prefab = prefab;
            m_grid = prefab.CubeGrids[0];
            m_boundingBox = m_grid.CalculateBoundingBox();
            m_mountPoints = new Dictionary<string, Dictionary<string, PartMount>>();
            ComputeMountPoints();
        }

        private void ComputeMountPoints()
        {
            m_mountPoints.Clear();
            foreach (var block in m_grid.CubeBlocks)
            {
                MyObjectBuilder_TextPanel f;
                string[] names;
                if (block is MyObjectBuilder_TerminalBlock)
                    names = new string[] { (block as MyObjectBuilder_TerminalBlock).CustomName, block.Name };
                else
                    names = new string[] { block.Name };
                foreach (var name in names)
                {
                    if (!name.StartsWith("Dummy ")) continue;
                    var parts = name.Split(' ');
                    if (parts.Length != 3) continue;
                    var spec = parts[1].Split(':');
                    if (spec.Length != 2) continue;

                    var mountType = spec[0];
                    var mountPiece = spec[1];
                    var mountName = parts[2];

                    Dictionary<string, PartMount> partsOfType;
                    if (!m_mountPoints.TryGetValue(mountType, out partsOfType))
                        partsOfType = m_mountPoints[mountType] = new Dictionary<string, PartMount>();
                    PartMount mount;
                    if (!partsOfType.TryGetValue(mountName, out mount))
                        mount = partsOfType[mountName] = new PartMount(this, mountType, mountName);

                    mount.Add(new PartMountPointBlock(mountPiece, block));
                    break;
                }
            }
        }
    }
}