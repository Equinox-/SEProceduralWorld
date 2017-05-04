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
        public readonly string m_piece;
        private readonly Base6Directions.Direction m_mountDirection;

        public MyPartMountPointBlock(string piece, MyObjectBuilder_CubeBlock block, string[] args)
        {
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

        private static int PermutationsOf(int n)
        {
            var perms = 1;
            for (var i = 2; i <= n; i++)
                perms *= i;
            return perms;
        }

        // https://antoinecomeau.blogspot.ca/2014/07/mapping-between-permutations-and.html
        public static void Permute<T>(ref int[] elems, ref IReadOnlyList<T> source, ref List<T> output, int m)
        {
            if (elems.Length == 1)
            {
                output[0] = source[0];
                return;
            }

            var n = elems.Length;
            for (var i = 0; i < n; i++)
                elems[i] = i;
            for (var i = 0; i < n; i++)
            {
                var ind = m % (n - i);
                m = m / (n - i);
                output[i] = source[elems[ind]];
                elems[ind] = elems[n - i - 1];
            }
        }

        private static IEnumerable<MatrixI> GetMultiMatches(IReadOnlyList<MyPartMountPointBlock> mine, IReadOnlyList<MyPartMountPointBlock> other)
        {
            var cache = new HashSet<MatrixI>();
            var match = Math.Min(mine.Count, other.Count);
            if (match == mine.Count)
            {
                var permute = new List<MyPartMountPointBlock>(mine);
                var temp = new int[mine.Count];
                var perms = PermutationsOf(mine.Count);
                for (var perm = 0; perm < perms; perm++)
                {
                    Permute(ref temp, ref mine, ref permute, perm);
                    for (var a = 0; a <= other.Count - match; a++)
                    {
                        cache.Clear();
                        permute[0].GetTransforms(other[a], cache);
                        for (var b = 1; b < match; b++)
                            cache.RemoveWhere(x => Vector3I.Transform(other[a + b].MountLocation, ref x) != (Vector3I)permute[b].m_block.Min);
                        foreach (var m in cache)
                            yield return m;
                    }
                }
            }
            else
            {
                var permute = new List<MyPartMountPointBlock>(other);
                var temp = new int[other.Count];
                var perms = PermutationsOf(other.Count);
                for (var perm = 0; perm < perms; perm++)
                {
                    Permute(ref temp, ref mine, ref permute, perm);
                    for (var a = 0; a <= mine.Count - match; a++)
                    {
                        cache.Clear();
                        mine[a].GetTransforms(permute[0], cache);
                        for (var b = 1; b < match; b++)
                            cache.RemoveWhere(x => Vector3I.Transform(permute[b].MountLocation, ref x) != (Vector3I)mine[a + b].m_block.Min);
                        foreach (var m in cache)
                            yield return m;
                    }
                }
            }
        }

        private static readonly MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>> m_mountCache = new MyCache<MyTuple<MyPartMount, MyPartMount>, HashSet<MatrixI>>(128);

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
                    mount.Add(new MyPartMountPointBlock(mountPiece, block, args));
                    break;
                }
            }
        }
    }
}