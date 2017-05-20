using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Utils;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Library
{
    public partial class MyPartStorage
    {
        public bool CubeExists(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) != ContainmentType.Disjoint && m_blocks.ContainsKey(pos);
        }

        public bool IsReserved(Vector3 pos, bool testShared, bool testOptional)
        {
            return ReservedSpace.Contains(pos) != ContainmentType.Disjoint && m_reservedSpaces.Any(x =>
                       (!x.IsShared || testShared) && (!x.IsOptional || testOptional) && x.Box.Contains(pos) != ContainmentType.Disjoint);
        }

        public virtual MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
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
            return IntersectsInternal(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional);
            //            var key = new IntersectKey(partA, partB, transformA, transformB, testOptional);
            //            bool result;
            //            // ReSharper disable once ConvertIfStatementToReturnStatement
            //            if (IntersectionCache.TryGet(key, out result))
            //                return result;
            //            return IntersectionCache.Store(key, IntersectsInternal(ref partA, ref transformA, ref invTransformA, ref partB, ref transformB, ref invTransformB, testOptional));
        }

        private struct IntersectKey
        {
            private readonly MyPart m_partA;
            private readonly MyPart m_partB;
            private readonly MatrixI m_transformA;
            private readonly MatrixI m_transformB;
            private readonly bool m_testOptional;

            public IntersectKey(MyPart partA, MyPart partB, MatrixI transformA, MatrixI transformB, bool testOptional)
            {
                m_partA = partA;
                m_partB = partB;
                m_transformA = transformA;
                m_transformB = transformB;
                m_testOptional = testOptional;
            }

            public override int GetHashCode()
            {
                return ((m_partA.GetHashCode() + m_transformA.GetHashCode() * 7919) * (m_partB.GetHashCode() + m_transformB.GetHashCode() * 7919)) ^ m_testOptional.GetHashCode();
            }

            public override bool Equals(object o)
            {
                if (!(o is IntersectKey)) return false;
                var other = (IntersectKey)o;
                return m_testOptional == other.m_testOptional && ((m_partA == other.m_partA && m_partB == other.m_partB && m_transformA.Equals(other.m_transformA) && m_transformB.Equals(other.m_transformB)) ||
                       (m_partA == other.m_partB && m_partB == other.m_partA && m_transformA.Equals(other.m_transformB) && m_transformB.Equals(other.m_transformA)));
            }
        }

        // Target a 16MB cache.
        private static readonly LRUCache<IntersectKey, bool> IntersectionCache = new LRUCache<IntersectKey, bool>(16 * 1024 * 1024 / (2 * 8 + 2 * 16 * 4));

        private static bool IntersectsInternal(ref MyPart partA, ref MatrixI transformA, ref MatrixI invTransformA, ref MyPart partB, ref MatrixI transformB, ref MatrixI invTransformB, bool testOptional)
        {
            using (partA.m_lock.AcquireSharedUsing())
            {
                using (partB.m_lock.AcquireSharedUsing())
                {
                    var reservedAAll = MyUtilities.TransformBoundingBox(partA.ReservedSpace, ref transformA);
                    var reservedBAll = MyUtilities.TransformBoundingBox(partB.ReservedSpace, ref transformB);

                    var reservedA = new List<MyTuple<MyReservedSpace, BoundingBox>>(partA.m_reservedSpaces.Count);
                    // ReSharper disable once LoopCanBeConvertedToQuery (preserve ref)
                    foreach (var aabb in partA.m_reservedSpaces)
                        if (!aabb.IsOptional || testOptional)
                            reservedA.Add(MyTuple.Create(aabb, MyUtilities.TransformBoundingBox(aabb.Box, ref transformA)));

                    var reservedB = new List<MyTuple<MyReservedSpace, BoundingBox>>(partB.m_reservedSpaces.Count);
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
            }
        }
    }
}
