using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace ProcBuild
{
    public class MyPartMountPointBlock
    {
        public readonly MyObjectBuilder_CubeBlock m_block;
        public readonly string m_piece;

        public MyPartMountPointBlock(string piece, MyObjectBuilder_CubeBlock block)
        {
            m_piece = piece;
            m_block = block;
        }

        public Base6Directions.Direction MountDirection6 => Base6Directions.GetOppositeDirection(Base6Directions.GetCross(m_block.BlockOrientation.Up, m_block.BlockOrientation.Forward));

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        public Vector3I MountLocation => m_block.Min + MountDirection;

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
                tmp.Translation = m_block.Min - Vector3I.TransformNormal(other.MountLocation, ref tmp);
                cache.Add(tmp);
            }
        }
    }

    public class MyPartMount
    {
        public readonly string m_mountType;
        public readonly string m_mountName;
        public readonly SortedDictionary<string, List<MyPartMountPointBlock>> m_blocks;
        private readonly MyPart m_part;

        public MyPartMount(MyPart part, string mountType, string mountName)
        {
            m_part = part;
            m_mountType = mountType;
            m_mountName = mountName;
            m_blocks = new SortedDictionary<string, List<MyPartMountPointBlock>>();
        }

        internal void Add(MyPartMountPointBlock block)
        {
            List<MyPartMountPointBlock> points;
            if (!m_blocks.TryGetValue(block.m_piece, out points))
                points = m_blocks[block.m_piece] = new List<MyPartMountPointBlock>(2);
            points.Add(block);
        }

        public HashSet<MatrixI> GetTransform(MyPartMount otherMount)
        {
            if (m_blocks.Count == 0) return null;
            var options = new HashSet<MatrixI>();
            var availableKeys = m_blocks.Keys.Union(otherMount.m_blocks.Keys);
            var init = false;
            foreach (var key in availableKeys)
            {
                foreach (var mine in m_blocks[key])
                    foreach (var other in otherMount.m_blocks[key])
                    {
                        if (!init)
                            mine.GetTransforms(other, options);
                        else
                            options.RemoveWhere(x => Vector3I.Transform(other.MountLocation, ref x) != (Vector3I)mine.m_block.Min);
                    }
                init = true;
            }
            return options.Count > 0 ? options : null;
        }
    }

    public class MyPart
    {
        public readonly MyPrefabDefinition m_prefab;
        public readonly MyObjectBuilder_CubeGrid m_grid;
        public readonly BoundingBoxI m_boundingBox;

        private readonly Dictionary<string, Dictionary<string, MyPartMount>> m_mountPoints;
        private readonly Dictionary<Vector3I, MyObjectBuilder_CubeBlock> m_blocks;
        private readonly Dictionary<MyComponentDefinition, int> m_componentCost;

        public MyPart(MyPrefabDefinition prefab)
        {
            m_prefab = prefab;
            m_grid = prefab.CubeGrids[0];
            m_componentCost = new Dictionary<MyComponentDefinition, int>();
            m_blocks = new Dictionary<Vector3I, MyObjectBuilder_CubeBlock>(m_grid.CubeBlocks.Count);
            var aabb = new BoundingBoxI();
            foreach (var block in m_grid.CubeBlocks)
            {
                aabb.Include(block.Min);
                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
                m_blocks[block.Min] = block;
                if (def == null) continue;
                var rot = MatrixI.CreateRotation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up, block.BlockOrientation.Forward, block.BlockOrientation.Up);
                aabb.Include(block.Min + Vector3I.Abs(Vector3I.Transform(def.Size - Vector3I.One, ref rot)));

                foreach (var c in def.Components)
                {
                    var cval = 0;
                    m_componentCost.TryGetValue(c.Definition, out cval);
                    m_componentCost[c.Definition] = cval + c.Count;
                }
            }
            m_boundingBox = aabb;
            m_mountPoints = new Dictionary<string, Dictionary<string, MyPartMount>>();
            ComputeMountPoints();
        }

        public IEnumerable<Vector3I> Occupied => m_blocks.Keys;

        public MyObjectBuilder_CubeBlock GetBlockAt(Vector3I pos)
        {
            MyObjectBuilder_CubeBlock block;
            return m_blocks.TryGetValue(pos, out block) ? block : null;
        }

        public bool CubeExists(Vector3I pos)
        {
            return m_blocks.ContainsKey(pos);
        }

        public IEnumerable<KeyValuePair<string, MyPartMount>> MountPoints => (m_mountPoints.Values.SelectMany(x => x));

        public MyPartMount MountPoint(string typeID, string instanceID)
        {
            Dictionary<string, MyPartMount> instan;
            m_mountPoints.TryGetValue(typeID, out instan);
            MyPartMount part = null;
            instan?.TryGetValue(instanceID, out part);
            return part;
        }

        private void ComputeMountPoints()
        {
            m_mountPoints.Clear();
            foreach (var block in m_grid.CubeBlocks)
            {
                string[] names;
                if (block is MyObjectBuilder_TerminalBlock)
                    names = new[] { (block as MyObjectBuilder_TerminalBlock).CustomName, block.Name };
                else
                    names = new[] { block.Name };
                foreach (var name in names)
                {
                    if (name == null) continue;
                    if (!name.StartsWith("Dummy ")) continue;
                    var parts = name.Split(' ');
                    if (parts.Length != 3) continue;
                    var spec = parts[1].Split(':');
                    if (spec.Length != 2) continue;

                    var mountType = spec[0];
                    var mountPiece = spec[1];
                    var mountName = parts[2];

                    Dictionary<string, MyPartMount> partsOfType;
                    if (!m_mountPoints.TryGetValue(mountType, out partsOfType))
                        partsOfType = m_mountPoints[mountType] = new Dictionary<string, MyPartMount>();
                    MyPartMount mount;
                    if (!partsOfType.TryGetValue(mountName, out mount))
                        mount = partsOfType[mountName] = new MyPartMount(this, mountType, mountName);

                    mount.Add(new MyPartMountPointBlock(mountPiece, block));
                    break;
                }
            }
        }
    }
}