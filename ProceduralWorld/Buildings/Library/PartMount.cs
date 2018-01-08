using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Cache;
using Equinox.Utils.Logging;
using Sandbox.Game.Lights;
using VRage;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public class PartMount
    {
        public string MountType { private set; get; }
        public string MountName { private set; get; }
        private readonly SortedDictionary<string, List<PartMountPointBlock>> m_blocks;
        public readonly PartMetadata Owner;

        public AdjacencyRule AdjacencyRule { private set; get; }

        public IEnumerable<PartMountPointBlock> Blocks => m_blocks.Values.SelectMany(x => x);
        private ILogging Logging => Owner.Manager;

        public PartMount(PartMetadata part, string mountType, string mountName)
        {
            Owner = part;
            MountType = mountType;
            MountName = mountName;
            AdjacencyRule = AdjacencyRule.Any;
            m_blocks = new SortedDictionary<string, List<PartMountPointBlock>>();
        }

        public void Init(Ob_Part.MountPoint v)
        {
            MountType = v.Type;
            MountName = v.Name;
            AdjacencyRule = v.AdjacencyRule;
            m_blocks.Clear();
            foreach (var block in v.Blocks)
            {
                var res = new PartMountPointBlock(this);
                res.Init(block);
                List<PartMountPointBlock> lst;
                if (!m_blocks.TryGetValue(res.Piece, out lst))
                    m_blocks[res.Piece] = lst = new List<PartMountPointBlock>();
                lst.Add(res);
            }
        }

        public Ob_Part.MountPoint GetObjectBuilder()
        {
            var res = new Ob_Part.MountPoint
            {
                Name = MountName,
                Type = MountType,
                AdjacencyRule = AdjacencyRule,
                Blocks = m_blocks.Values.SelectMany(x => x).Select(x => x.GetObjectBuilder()).ToArray()
            };
            return res;
        }

        internal void Add(PartMountPointBlock block)
        {
            List<PartMountPointBlock> points;
            if (!m_blocks.TryGetValue(block.Piece, out points))
                points = m_blocks[block.Piece] = new List<PartMountPointBlock>(2);
            points.Add(block);
            if (block.AdjacencyRule > AdjacencyRule)
                AdjacencyRule = block.AdjacencyRule;
        }

        private static void GetMultiMatches(IReadOnlyList<PartMountPointBlock> mine, IReadOnlyList<PartMountPointBlock> other, HashSet<MatrixI> cache)
        {
            cache.Clear();
            var match = Math.Min(mine.Count, other.Count);
            if (match == mine.Count)
            {
                foreach (var ot in other)
                    mine[0].GetTransforms(ot, cache);
                MatrixI inv;
                cache.RemoveWhere(x =>
                {
                    MatrixI.Invert(ref x, out inv);
                    for (var i = 1; i < mine.Count; i++)
                    {
                        var loc = Vector3I.Transform(mine[i].MountLocation, ref inv);
                        var contains = false;
                        foreach (var y in other)
                        {
                            var opposLoc = y.AnchorLocation;
                            if (opposLoc != loc) continue;
                            contains = true;
                            break;
                        }
                        if (!contains) return true;
                    }
                    return false;
                });
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
                        var contains = false;
                        foreach (var y in mine)
                        {
                            var opposLoc = y.AnchorLocation;
                            if (opposLoc != loc) continue;
                            contains = true;
                            break;
                        }
                        if (!contains) return true;
                    }
                    return false;
                });
            }
        }

        // Computing mount point transforms is pretty expensive, so we want a fairly large cache.
        // ~256 bytes per entry.  Target a 32MB cache
        private static readonly LruCache<MyTuple<PartMount, PartMount>, HashSet<MatrixI>> MountCache =
            new LruCache<MyTuple<PartMount, PartMount>, HashSet<MatrixI>>(32 * 1024 * 1024 / 256, new TupleEqualityComparer<PartMount, PartMount>(null, null));

        private static HashSet<MatrixI> GetTransformInternal(MyTuple<PartMount, PartMount> meOther)
        {
            var me = meOther.Item1;
            var other = meOther.Item2;
            if (me.m_blocks.Count == 0 || other.m_blocks.Count == 0) return null;
            var adjacencyRule = me.AdjacencyRule > other.AdjacencyRule ? me.AdjacencyRule : other.AdjacencyRule;
            if (adjacencyRule == AdjacencyRule.ExcludeSelfPrefab && me.Owner == other.Owner) return null;
            if (adjacencyRule == AdjacencyRule.ExcludeSelfMount && me == other) return null;

            // get transforms where all pieces line up.
            // every A must match to an A, etc.
            var keyCache = new HashSet<MatrixI>(MatrixIEqualityComparer.Instance);
            var options = new HashSet<MatrixI>(MatrixIEqualityComparer.Instance);
            var availableKeys = me.m_blocks.Keys.Union(other.m_blocks.Keys);
            var init = false;
            foreach (var key in availableKeys)
            {
                GetMultiMatches(me.m_blocks[key], other.m_blocks[key], keyCache);
                if (!init)
                    foreach (var x in keyCache)
                        options.Add(x);
                else
                    options.RemoveWhere(x => !keyCache.Contains(x));
                if (options.Count <= 0) break;
                init = true;
            }
            return options.Count > 0 ? options : null;
        }

        public IEnumerable<MatrixI> GetTransform(PartMount otherMount)
        {
            HashSet<MatrixI> output;
            if (MountCache.TryGet(MyTuple.Create(otherMount, this), out output))
                return output?.Select(x =>
                {
                    MatrixI val;
                    MatrixI.Invert(ref x, out val);
                    return val;
                });
            return MountCache.GetOrCreate(MyTuple.Create(this, otherMount), GetTransformInternal);
        }

        // In order to close off this mount point we need at least this region to be free (in part block space)
        private MyTuple<PartFromPrefab, MatrixI>? m_smallestTerminalAttachment = null;
        /// <summary>
        /// Gives a best guess on the smallest possible attachment configuration.
        /// </summary>
        public MyTuple<PartFromPrefab, MatrixI> SmallestTerminalAttachment
        {
            get
            {
                if (!m_smallestTerminalAttachment.HasValue)
                    m_smallestTerminalAttachment = ComputeSmallestTerminalAttachment();
                return m_smallestTerminalAttachment.Value;
            }
        }

        internal void InvalidateSmallestAttachment()
        {
            m_smallestTerminalAttachment = null;
        }

        private MyTuple<PartFromPrefab, MatrixI> ComputeSmallestTerminalAttachment()
        {
            foreach (var part in Owner.Manager.SortedBySize)
                if (part.MountPointsOfType(MountType).Count() <= 2)
                    foreach (var mount in part.MountPointsOfType(MountType))
                    {
                        var transforms = GetTransform(mount);
                        if (transforms == null) continue;
                        foreach (var transform in transforms)
                            return MyTuple.Create(part, transform);
                    }
            foreach (var part in Owner.Manager.SortedBySize)
                foreach (var mount in part.MountPointsOfType(MountType))
                {
                    var transforms = GetTransform(mount);
                    if (transforms == null) continue;
                    foreach (var transform in transforms)
                    {
                        Logging.Error("Failed to find any terminal module that is attachable to \"{1} {2}\" on {0}.  Resorting to {3}.", Owner.Name, MountType, MountName, part.Name);
                        return MyTuple.Create(part, transform);
                    }
                }
            Logging.Error("Failed to find any module that is attachable to \"{1} {2}\" on {0}", Owner.Name, MountType, MountName);
            return MyTuple.Create((PartFromPrefab)null, default(MatrixI));
        }
    }
}