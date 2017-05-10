using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProcBuild.Library;
using ProcBuild.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Library.Collections;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace ProcBuild
{
    public struct ReservedSpace
    {
        public BoundingBox Box;
        public bool IsShared;
        public bool IsOptional;
    }

    public class MyPart
    {
        public const string MOUNT_PREFIX = "Dummy";
        public const string RESERVED_SPACE_PREFIX = "ReservedSpace";
        public const string MULTI_USE_SENTINEL = "&&";

        private readonly List<ReservedSpace> m_reservedSpaces;

        private readonly Dictionary<string, Dictionary<string, MyPartMount>> m_mountPoints;
        private readonly Dictionary<Vector3I, MyObjectBuilder_CubeBlock> m_blocks;
        private readonly Dictionary<Vector3I, MyPartMountPointBlock> m_mountPointBlocks;
        private readonly Dictionary<MyComponentDefinition, int> m_componentCost;
        private readonly Dictionary<MyDefinitionId, int> m_blockCountByType;
        private readonly Dictionary<MyStringHash, float> m_powerConsumptionByGroup;

        public MyPart(MyPrefabDefinition prefab)
        {
            Prefab = prefab;
            PrimaryGrid = prefab.CubeGrids[0];
            m_componentCost = new Dictionary<MyComponentDefinition, int>(64);
            m_blockCountByType = new Dictionary<MyDefinitionId, int>(128, MyDefinitionId.Comparer);
            m_powerConsumptionByGroup = new Dictionary<MyStringHash, float>(16, MyStringHash.Comparer);
            m_mountPoints = new Dictionary<string, Dictionary<string, MyPartMount>>();
            m_mountPointBlocks = new Dictionary<Vector3I, MyPartMountPointBlock>(128, Vector3I.Comparer);
            m_reservedSpaces = new List<ReservedSpace>();
            m_blocks = new Dictionary<Vector3I, MyObjectBuilder_CubeBlock>(Prefab.CubeGrids.Select(x => x.CubeBlocks.Count * 2).Sum(), Vector3I.Comparer);

            ComputeBlockMap();
            ComputeReservedSpace();
            ComputeMountPoints();

            SessionCore.Log("Loaded {0} with {1} mount points, {2} reserved spaces, and {3} blocks.  {4} aux grids", Name, MountPoints.Count(), m_reservedSpaces.Count, PrimaryGrid.CubeBlocks.Count, Prefab.CubeGrids.Length - 1);
            foreach (var type in MountPointTypes)
                SessionCore.Log("    ...of type \"{0}\" there are {1}", type, MountPointsOfType(type).Count());
        }


        #region Mount Points
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
        #endregion

        public BoundingBox BoundingBox { get; private set; }

        public BoundingBox ReservedSpace { get; private set; }

        public BoundingBox BoundingBoxBoth
        {
            get
            {
                var box = new BoundingBox(BoundingBox.Min, BoundingBox.Max);
                box = box.Include(ReservedSpace.Min);
                box = box.Include(ReservedSpace.Max);
                return box;
            }
        }

        public IEnumerable<ReservedSpace> ReservedSpaces => m_reservedSpaces;

        public MyCubeSize PrimaryCubeSize => PrimaryGrid.GridSizeEnum;

        public MyObjectBuilder_CubeGrid PrimaryGrid { get; }

        public MyPrefabDefinition Prefab { get; }

        public string Name => Prefab.Id.SubtypeName.Substring(MyPartManager.PREFAB_NAME_PREFIX.Length);

        #region Intersection
        public bool CubeExists(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) != ContainmentType.Disjoint && m_blocks.ContainsKey(pos);
        }

        public bool IsReserved(Vector3 pos, bool testShared, bool testOptional)
        {
            return ReservedSpace.Contains(pos) != ContainmentType.Disjoint && m_reservedSpaces.Any(x =>
                (!x.IsShared || testShared) && (!x.IsOptional || testOptional) && x.Box.Contains(pos) != ContainmentType.Disjoint);
        }
        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            if (BoundingBox.Contains((Vector3)pos) == ContainmentType.Disjoint)
                return null;
            MyObjectBuilder_CubeBlock block;
            return m_blocks.TryGetValue(pos, out block) ? block : null;
        }

        public static bool Intersects(MyPart partA, MatrixI transformA, MatrixI invTransformA, MyPart partB, MatrixI transformB, MatrixI invTransformB, bool testOptional)
        {
            return Intersects(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional);
        }

        public static bool Intersects(MyPart partA, MatrixI transformA, MatrixI invTransformA, ref MyPart partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional)
        {
            return Intersects(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional);
        }

        public static bool Intersects(ref MyPart partA, ref MatrixI transformA, ref MatrixI invTransformA, ref MyPart partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional)
        {
            var reservedAAll = MyUtilities.TransformBoundingBox(partA.ReservedSpace, ref transformA);
            var reservedBAll = MyUtilities.TransformBoundingBox(partB.ReservedSpace, ref transformB);

            var reservedA = new List<MyTuple<ReservedSpace, BoundingBox>>(partA.m_reservedSpaces.Count);
            // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
            foreach (var aabb in partA.m_reservedSpaces)
                if (!aabb.IsOptional || testOptional)
                    reservedA.Add(MyTuple.Create(aabb, MyUtilities.TransformBoundingBox(aabb.Box, ref transformA)));

            var reservedB = new List<MyTuple<ReservedSpace, BoundingBox>>(partB.m_reservedSpaces.Count);
            // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
            foreach (var aabb in partB.m_reservedSpaces)
                if (!aabb.IsOptional || testOptional)
                    reservedB.Add(MyTuple.Create(aabb, MyUtilities.TransformBoundingBox(aabb.Box, ref transformB)));

            // Reserved spaces intersect?
            if (partA.m_reservedSpaces.Count > 0 && partB.m_reservedSpaces.Count > 0 && reservedAAll.Intersects(reservedBAll))
                if (reservedA.Any(x => reservedB.Any(y => !y.Item1.IsShared && !x.Item1.IsShared && x.Item2.Intersects(y.Item2))))
                    return true;

            var blockAAll = MyUtilities.TransformBoundingBox(partA.BoundingBox, ref transformA);
            var blockBAll = MyUtilities.TransformBoundingBox(partB.BoundingBox, ref transformB);

            // Block spaces intersect with reserved space?
            if (partA.m_reservedSpaces.Count > 0 && reservedAAll.Intersects(blockBAll))
                foreach (var aabb in reservedA)
                {
                    var min = Vector3I.Floor(Vector3.Max(aabb.Item2.Min, blockBAll.Min));
                    var max = Vector3I.Ceiling(Vector3.Min(aabb.Item2.Max, blockBAll.Max));
                    for (var vi = new Vector3I_RangeIterator(ref min, ref max); vi.IsValid(); vi.MoveNext())
                        if (partB.CubeExists(Vector3I.Transform(vi.Current, invTransformB)))
                            return true;
                }
            if (partB.m_reservedSpaces.Count > 0 && reservedBAll.Intersects(blockAAll))
                foreach (var aabb in reservedB)
                {
                    var min = Vector3I.Floor(Vector3.Max(aabb.Item2.Min, blockAAll.Min));
                    var max = Vector3I.Ceiling(Vector3.Min(aabb.Item2.Max, blockAAll.Max));
                    for (var vi = new Vector3I_RangeIterator(ref min, ref max); vi.IsValid(); vi.MoveNext())
                        if (partA.CubeExists(Vector3I.Transform(vi.Current, invTransformA)))
                            return true;
                }

            // Block space intersects with block space?
            if (!blockAAll.Intersects(blockBAll)) return false;
            if (partA.m_blocks.Count < partB.m_blocks.Count)
            {
                foreach (var pos in partA.m_blocks.Keys)
                    if (partB.CubeExists(Vector3I.Transform(Vector3I.Transform(pos, ref transformA), ref invTransformB)))
                        return true;
            }
            else
            {
                foreach (var pos in partB.m_blocks.Keys)
                    if (partA.CubeExists(Vector3I.Transform(Vector3I.Transform(pos, ref transformB), ref invTransformA)))
                        return true;
            }
            return false;
        }
        #endregion

        #region MetaCompute
        private void ComputeReservedSpace()
        {
            m_reservedSpaces.Clear();
            ReservedSpace = new BoundingBox();
            foreach (var block in PrimaryGrid.CubeBlocks)
                foreach (var name in block.ConfigNames())
                {
                    if (!name.StartsWithICase(RESERVED_SPACE_PREFIX)) continue;
                    // SessionCore.Log("Loading reserved space block \"{0}\"", name);
                    var args = name.Substring(RESERVED_SPACE_PREFIX.Length).Trim().Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                    var box = MyPartDummyUtils.ParseReservedSpace(MyDefinitionManager.Static.GetCubeSize(PrimaryGrid.GridSizeEnum), block, args, SessionCore.Log);
                    box.Box.Max += (Vector3I)block.Min;
                    box.Box.Min += (Vector3I)block.Min;
                    if (m_reservedSpaces.Count == 0)
                        ReservedSpace = new BoundingBox(box.Box.Min, box.Box.Max);
                    ReservedSpace = ReservedSpace.Include(box.Box.Min);
                    ReservedSpace = ReservedSpace.Include(box.Box.Max);
                    m_reservedSpaces.Add(box);
                }
        }

        private void ComputeMountPoints()
        {
            m_mountPoints.Clear();
            foreach (var block in PrimaryGrid.CubeBlocks)
            {
                foreach (var name in block.ConfigNames())
                {
                    if (!name.StartsWithICase(MOUNT_PREFIX)) continue;
                    var parts = name.Substring(MOUNT_PREFIX.Length).Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                    if (parts.Length < 3) continue;
                    var spec = parts[0].Split(':');
                    if (spec.Length != 2) continue;

                    var mountType = spec[0];
                    var mountPiece = spec[1];
                    var mountName = parts[1];

                    Dictionary<string, MyPartMount> partsOfType;
                    if (!m_mountPoints.TryGetValue(mountType, out partsOfType))
                        partsOfType = m_mountPoints[mountType] = new Dictionary<string, MyPartMount>();
                    MyPartMount mount;
                    if (!partsOfType.TryGetValue(mountName, out mount))
                        mount = partsOfType[mountName] = new MyPartMount(this, mountType, mountName);

                    var args = new string[parts.Length - 2];
                    for (var i = 2; i < parts.Length; i++)
                        args[i - 2] = parts[i];
                    mount.Add(new MyPartMountPointBlock(mount, mountPiece, block, args));
                    break;
                }
            }

            m_mountPointBlocks.Clear();
            foreach (var mount in MountPoints)
                foreach (var block in mount.m_blocks.Values.SelectMany(x => x))
                    m_mountPointBlocks[block.AnchorLocation] = block;
        }

        private static readonly MyCache<MyDefinitionId, MyCubeBlockDefinition> m_cubeBlockDefCache = new MyCache<MyDefinitionId, MyCubeBlockDefinition>(128);
        private void ComputeBlockMap()
        {
            m_blocks.Clear();
            m_blockCountByType.Clear();
            m_componentCost.Clear();
            m_powerConsumptionByGroup.Clear();

            BoundingBox = new BoundingBox((Vector3I)PrimaryGrid.CubeBlocks[0].Min, (Vector3I)PrimaryGrid.CubeBlocks[0].Min);

            foreach (var grid in Prefab.CubeGrids)
                foreach (var block in grid.CubeBlocks)
                {
                    var blockID = block.GetId();
                    var def = m_cubeBlockDefCache.GetOrCreate(blockID, MyDefinitionManager.Static.GetCubeBlockDefinition);
                    if (grid == PrimaryGrid)
                    {
                        Vector3I blockMin = block.Min;
                        Vector3I blockMax;
                        BlockTransformations.ComputeBlockMax(block, ref def, out blockMax);
                        BoundingBox = BoundingBox.Include(blockMin);
                        BoundingBox = BoundingBox.Include(blockMax);
                        for (var rangeItr = new Vector3I_RangeIterator(ref blockMin, ref blockMax); rangeItr.IsValid(); rangeItr.MoveNext())
                            m_blocks[rangeItr.Current] = block;
                    }
                    if (def == null) continue;

                    m_blockCountByType[def.Id] = m_blockCountByType.GetValueOrDefault(def.Id, 0) + 1;

                    foreach (var c in def.Components)
                        m_componentCost[c.Definition] = m_componentCost.GetValueOrDefault(c.Definition, 0) + c.Count;

                    var powerUsage = PowerUtilities.MaxPowerConsumption(def);
                    if (Math.Abs(powerUsage.Item2) > 1e-8)
                        m_powerConsumptionByGroup[powerUsage.Item1] = m_powerConsumptionByGroup.GetValueOrDefault(powerUsage.Item1, 0) + powerUsage.Item2;
                }
        }
        #endregion
    }
}