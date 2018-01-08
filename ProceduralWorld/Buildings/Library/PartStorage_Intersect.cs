using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Cache;
using VRage;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public partial class PartMetadata
    {
        public bool CubeExists(Vector3I pos)
        {
            using (LockSharedUsing())
                return BoundingBox.Contains((Vector3)pos) != ContainmentType.Disjoint && m_blocks.ContainsKey(pos);
        }

        public bool IsReserved(Vector3 pos, bool testShared, bool testOptional)
        {
            using (LockSharedUsing())
                return ReservedSpace.Contains(pos) != ContainmentType.Disjoint && m_reservedSpaces.Any(x =>
                           (!x.IsShared || testShared) && (!x.IsOptional || testOptional) && x.Box.Contains(pos) != ContainmentType.Disjoint);
        }

        public virtual MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            using (LockSharedUsing())
            {
                if (BoundingBox.Contains((Vector3)pos) == ContainmentType.Disjoint)
                    return null;
                MyObjectBuilder_CubeBlock block;
                return m_blocks.TryGetValue(pos, out block) ? block : null;
            }
        }

        public static bool Intersects(PartFromPrefab partA, MatrixI transformA, MatrixI invTransformA, PartFromPrefab partB, MatrixI transformB, MatrixI invTransformB, bool testOptional, bool testQuick = false)
        {
            return Intersects(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional, testQuick);
        }

        public static bool Intersects(PartFromPrefab partA, MatrixI transformA, MatrixI invTransformA, ref PartFromPrefab partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional, bool testQuick = false)
        {
            return Intersects(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional, testQuick);
        }

        public static bool Intersects(ref PartFromPrefab partA, ref MatrixI transformA, ref MatrixI invTransformA, ref PartFromPrefab partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional, bool testQuick = false)
        {
            var cheapResult = IntersectsInternalCheap(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional);
            switch (cheapResult)
            {
                case CheapIntersection.Yes:
                    return true;
                case CheapIntersection.No:
                    return false;
                case CheapIntersection.Maybe:
                default:
                    break;
            }
            if (testQuick) return true;

            MatrixI abTransform;
            MatrixI.Multiply(ref transformA, ref invTransformB, out abTransform);
            var key = new IntersectKey(partA, partB, abTransform, testOptional);
            bool result;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            // TODO when threading cause a wait when another thread is preparing this cache value?
            if (IntersectionCache.TryGet(key, out result))
                return result;
            return IntersectionCache.Store(key, IntersectsInternalExpensive(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional));
        }

        private struct IntersectKey
        {
            private readonly PartFromPrefab m_partA;
            private readonly PartFromPrefab m_partB;
            private readonly MatrixI m_transformAB;
            private readonly bool m_testOptional;

            public IntersectKey(PartFromPrefab partA, PartFromPrefab partB, MatrixI transformAB, bool testOptional)
            {
                m_partA = partA;
                m_partB = partB;
                m_transformAB = transformAB;
                m_testOptional = testOptional;
            }

            public override int GetHashCode()
            {
                return (m_partA.GetHashCode() * 7919) ^ (m_partB.GetHashCode() * 13) ^ (m_transformAB.GetHashCode() ^ 8175) ^ m_testOptional.GetHashCode();
            }

            public bool Equals(IntersectKey other)
            {
                return m_testOptional == other.m_testOptional && m_partA == other.m_partA && m_partB == other.m_partB &&
                       m_transformAB.Equals(other.m_transformAB);
            }

            public override bool Equals(object o)
            {
                if (!(o is IntersectKey)) return false;
                return Equals((IntersectKey)o);
            }
        }

        private class IntersectKeyEquality : IEqualityComparer<IntersectKey>
        {
            public bool Equals(IntersectKey x, IntersectKey y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(IntersectKey obj)
            {
                return obj.GetHashCode();
            }
        }

        // Target a 16MB cache.
        private static readonly LruCache<IntersectKey, bool> IntersectionCache = new LruCache<IntersectKey, bool>(16 * 1024 * 1024 / (2 * 8 + 2 * 16 * 4), new IntersectKeyEquality());

        private enum CheapIntersection
        {
            Yes = 0,
            No,
            Maybe
        }
        private static CheapIntersection IntersectsInternalCheap(ref PartFromPrefab partA, ref MatrixI transformA, ref MatrixI invTransformA, ref PartFromPrefab partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional)
        {
            using (partA.LockSharedUsing())
            using (partB.LockSharedUsing())
            {
                var reservedAAll = Utilities.TransformBoundingBox(partA.ReservedSpace, ref transformA);
                var reservedBAll = Utilities.TransformBoundingBox(partB.ReservedSpace, ref transformB);

                var reservedA = new List<MyTuple<ReservedSpace, BoundingBox>>(partA.m_reservedSpaces.Count);
                // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
                foreach (var aabb in partA.m_reservedSpaces)
                    if (!aabb.IsOptional || testOptional)
                        reservedA.Add(MyTuple.Create(aabb, Utilities.TransformBoundingBox(aabb.Box, ref transformA)));

                var reservedB = new List<MyTuple<ReservedSpace, BoundingBox>>(partB.m_reservedSpaces.Count);
                // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
                foreach (var aabb in partB.m_reservedSpaces)
                    if (!aabb.IsOptional || testOptional)
                        reservedB.Add(MyTuple.Create(aabb, Utilities.TransformBoundingBox(aabb.Box, ref transformB)));

                // Reserved spaces intersect?
                if (partA.m_reservedSpaces.Count > 0 && partB.m_reservedSpaces.Count > 0 && reservedAAll.Intersects(reservedBAll))
                    if (reservedA.Any(x => reservedB.Any(y => !y.Item1.IsShared && !x.Item1.IsShared && x.Item2.Intersects(y.Item2))))
                        return CheapIntersection.Yes;


                var blockAAll = Utilities.TransformBoundingBox(partA.BoundingBox, ref transformA);
                var blockBAll = Utilities.TransformBoundingBox(partB.BoundingBox, ref transformB);

                // Block spaces intersect with reserved space?
                if (partA.m_reservedSpaces.Count > 0 && reservedAAll.Intersects(blockBAll))
                    if (reservedA.Any(aabb => aabb.Item2.Intersects(blockBAll)))
                        return CheapIntersection.Maybe;
                // ReSharper disable once InvertIf
                if (partB.m_reservedSpaces.Count > 0 && reservedBAll.Intersects(blockAAll))
                    if (reservedB.Any(aabb => aabb.Item2.Intersects(blockAAll)))
                        return CheapIntersection.Maybe;

                // Block space intersects with block space?
                return !blockAAll.Intersects(blockBAll) ? CheapIntersection.No : CheapIntersection.Maybe;
            }
        }

        private static bool IntersectsInternalExpensive(ref PartFromPrefab partA, ref MatrixI transformA, ref MatrixI invTransformA, ref PartFromPrefab partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional)
        {
            using (partA.LockSharedUsing())
            using (partB.LockSharedUsing())
            {
                var reservedAAll = Utilities.TransformBoundingBox(partA.ReservedSpace, ref transformA);
                var reservedBAll = Utilities.TransformBoundingBox(partB.ReservedSpace, ref transformB);

                var reservedA = new List<MyTuple<ReservedSpace, BoundingBox>>(partA.m_reservedSpaces.Count);
                // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
                foreach (var aabb in partA.m_reservedSpaces)
                    if (!aabb.IsOptional || testOptional)
                        reservedA.Add(MyTuple.Create(aabb, Utilities.TransformBoundingBox(aabb.Box, ref transformA)));

                var reservedB = new List<MyTuple<ReservedSpace, BoundingBox>>(partB.m_reservedSpaces.Count);
                // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
                foreach (var aabb in partB.m_reservedSpaces)
                    if (!aabb.IsOptional || testOptional)
                        reservedB.Add(MyTuple.Create(aabb, Utilities.TransformBoundingBox(aabb.Box, ref transformB)));

                // Reserved spaces intersect?
                if (partA.m_reservedSpaces.Count > 0 && partB.m_reservedSpaces.Count > 0 && reservedAAll.Intersects(reservedBAll))
                    if (reservedA.Any(x => reservedB.Any(y => !y.Item1.IsShared && !x.Item1.IsShared && x.Item2.Intersects(y.Item2))))
                        return true;

                var blockAAll = Utilities.TransformBoundingBox(partA.BoundingBox, ref transformA);
                var blockBAll = Utilities.TransformBoundingBox(partB.BoundingBox, ref transformB);

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
        }
    }
}
