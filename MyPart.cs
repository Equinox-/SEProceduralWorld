using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;
using VRage;

namespace ProcBuild
{
    // Supported Mount Point Options
    // Name Format: "Dummy Type:Piece Name arguments..."

    public class MyPartMountPointBlock
    {
        public readonly MyObjectBuilder_CubeBlock m_block;
        public MyPartMount Owner { private set; get; }
        public readonly string m_piece;
        private readonly Base6Directions.Direction m_mountDirection;

        public MyPartMountPointBlock(MyPartMount owner, string piece, MyObjectBuilder_CubeBlock block, string[] args)
        {
            Owner = owner;
            m_piece = piece;
            m_block = block;
            m_mountDirection = Base6Directions.GetOppositeDirection(Base6Directions.GetCross(m_block.BlockOrientation.Up, m_block.BlockOrientation.Forward));
            foreach (var arg in args)
            {
                if (arg.StartsWith("D:"))
                {
                    if (!Enum.TryParse(arg.Substring(2), out m_mountDirection))
                        SessionCore.Instance.Logger.Log("Failed to parse mount point direction argument \"{0}\"", arg);
                }
                else
                    SessionCore.Instance.Logger.Log("Failed to parse mount point argument \"{0}\"", arg);
            }
        }

        public Base6Directions.Direction MountDirection6 => m_mountDirection;

        public Vector3I MountDirection => Base6Directions.GetIntVector(MountDirection6);

        public Vector3I AnchorLocation => m_block.Min;

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

        private static IEnumerable<MatrixI> GetMultiMatches(IReadOnlyList<MyPartMountPointBlock> mine, IReadOnlyList<MyPartMountPointBlock> other)
        {
            var cache = new HashSet<MatrixI>();
            var match = Math.Min(mine.Count, other.Count);
            if (match == mine.Count)
            {
                foreach (var ot in other)
                    mine[0].GetTransforms(ot, cache);
                cache.RemoveWhere(x =>
                {
                    MatrixI inv;
                    MatrixI.Invert(ref x, out inv);
                    for (var i = 1; i < mine.Count; i++)
                    {
                        var invLoc = Vector3I.Transform(mine[i].MountLocation, ref inv);
                        if (!other.Select(y => y.AnchorLocation).Contains(invLoc)) return true;
                    }
                    return false;
                });
                return cache;
            }
            else
            {
                foreach (var mi in mine)
                    mi.GetTransforms(other[0], cache);
                cache.RemoveWhere(x =>
                {
                    for (var i = 1; i < other.Count; i++)
                    {
                        var loc = Vector3I.Transform(other[i].MountLocation, ref x);
                        if (!mine.Select(y => y.AnchorLocation).Contains(loc)) return true;
                    }
                    return false;
                });
                return cache;
            }
        }

        // ~256 bytes per entry.  Target a 1MB cache
        private static readonly MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>> m_mountCache = new MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>>(4096);

        private static HashSet<MatrixI> GetTransformInternal(MyTuple<MyPartMount, MyPartMount> meOther)
        {
            var me = meOther.Item1;
            var other = meOther.Item2;
            if (me.m_blocks.Count == 0 || other.m_blocks.Count == 0) return null;
            // get transforms where all pieces line up.
            // every A must match to an A, etc.
            var options = new HashSet<MatrixI>();
            var availableKeys = me.m_blocks.Keys.Union(other.m_blocks.Keys);
            var init = false;
            foreach (var key in availableKeys)
            {
                var possible = new HashSet<MatrixI>(GetMultiMatches(me.m_blocks[key], other.m_blocks[key]));
                if (!init)
                    options = possible;
                else
                    options.RemoveWhere(x => !possible.Contains(x));
                init = true;
            }
            return options.Count > 0 ? options : null;
        }

        public IEnumerable<MatrixI> GetTransform(MyPartMount otherMount)
        {
            HashSet<MatrixI> output;
            if (m_mountCache.TryGet(MyTuple.Create(otherMount, this), out output))
                return output.Select(x =>
                {
                    MatrixI val;
                    MatrixI.Invert(ref x, out val);
                    return val;
                });
            return m_mountCache.GetOrCreate(MyTuple.Create(this, otherMount), GetTransformInternal);
        }
    }

    public class MyPart
    {
        public readonly MyPrefabDefinition m_prefab;
        public readonly MyObjectBuilder_CubeGrid m_grid;
        public readonly BoundingBoxI m_boundingBox;

        private readonly Dictionary<string, Dictionary<string, MyPartMount>> m_mountPoints;
        private readonly Dictionary<Vector3I, MyObjectBuilder_CubeBlock> m_blocks;
        private readonly Dictionary<Vector3I, MyPartMountPointBlock> m_mountPointBlocks;
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
                Vector3I blockMin = block.Min;
                Vector3I blockMax;
                BlockTransformations.ComputeBlockMax(block, out blockMax);
                for (var rangeItr = new Vector3I_RangeIterator(ref blockMin, ref blockMax); rangeItr.IsValid(); rangeItr.MoveNext())
                {
                    aabb.Include(rangeItr.Current);
                    m_blocks[rangeItr.Current] = block;
                }

                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
                m_blocks[block.Min] = block;
                if (def == null) continue;
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

        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            MyObjectBuilder_CubeBlock block;
            return m_blocks.TryGetValue(pos, out block) ? block : null;
        }

        public bool CubeExists(Vector3I pos)
        {
            return m_blocks.ContainsKey(pos);
        }

        public IEnumerable<string> MountPointTypes => m_mountPoints.Keys;

        public IEnumerable<MyPartMount> MountPoints => (m_mountPoints.Values.SelectMany(x => x.Values));

        public IEnumerable<MyPartMount> MountPointsOfType(string type)
        {
            Dictionary<string, MyPartMount> typed;
            return m_mountPoints.TryGetValue(type, out typed) ? typed.Values : Enumerable.Empty<MyPartMount>();
        }

        public MyPartMountPointBlock MountPointAt(Vector3I pos)
        {
            MyPartMountPointBlock mount;
            return m_mountPointBlocks.TryGetValue(pos, out mount) ? mount : null;
        }

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
                    if (parts.Length < 3) continue;
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

                    var args = new string[parts.Length - 3];
                    for (var i = 3; i < parts.Length; i++)
                        args[i - 3] = parts[i];
                    mount.Add(new MyPartMountPointBlock(mount, mountPiece, block, args));
                    break;
                }
            }

            m_mountPointBlocks.Clear();
            foreach (var mount in MountPoints)
                foreach (var block in mount.m_blocks.Values.SelectMany(x => x))
                    m_mountPointBlocks[block.AnchorLocation] = block;
        }
    }
}