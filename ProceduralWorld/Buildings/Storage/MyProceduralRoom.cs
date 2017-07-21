using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.Utils;
using VRage;
using VRage.Game;
using VRage.Library.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    public class MyProceduralRoom
    {
        private static readonly FastResourceLock MaxIDLock = new FastResourceLock();
        private static int MaxID = 0;

        public MyProceduralConstruction Owner { get; private set; }
        internal int m_aabbProxyID;

        public int RoomID { get; private set; }
        public MyPartFromPrefab Part => m_part;
        private MyPartFromPrefab m_part;
        private MatrixI m_transform, m_invTransform;
        private Dictionary<string, Dictionary<string, MyProceduralMountPoint>> m_mountPoints;

        public event Action AddedToConstruction, RemovedFromConstruction;

        public MyProceduralRoom()
        {
            Owner = null;
            using (MaxIDLock.AcquireExclusiveUsing())
                RoomID = MaxID++;
        }

        internal void Orphan()
        {
            if (Owner != null) RemovedFromConstruction?.Invoke();
            Owner = null;
        }

        internal void TakeOwnership(MyProceduralConstruction parent)
        {
            Orphan();
            if (parent == null) return;
            Owner = parent;
            parent.RegisterRoom(this);
            AddedToConstruction?.Invoke();
        }

        public void Init(MatrixI transform, MyPartFromPrefab prefab)
        {
            m_part = prefab;
            Transform = transform;
            m_mountPoints = new Dictionary<string, Dictionary<string, MyProceduralMountPoint>>();
            using (m_part.LockSharedUsing())
                foreach (var mount in prefab.MountPoints)
                {
                    var point = new MyProceduralMountPoint();
                    point.Init(mount, this);
                    Dictionary<string, MyProceduralMountPoint> byName;
                    if (!m_mountPoints.TryGetValue(point.MountPoint.MountType, out byName))
                        m_mountPoints[point.MountPoint.MountType] = byName = new Dictionary<string, MyProceduralMountPoint>();
                    byName[point.MountPoint.MountName] = point;
                }
        }

        public void Init(MyObjectBuilder_ProceduralRoom ob, MyProceduralConstruction parent)
        {
            RoomID = parent != null ? ob.RoomID : -1;
            m_part = SessionCore.Instance.PartManager.LoadNullable(ob.PrefabID);
            Transform = ob.Transform;
            m_mountPoints = new Dictionary<string, Dictionary<string, MyProceduralMountPoint>>();
            foreach (var mount in ob.MountPoints)
            {
                var point = new MyProceduralMountPoint();
                point.Init(mount, this);
                Dictionary<string, MyProceduralMountPoint> byName;
                if (!m_mountPoints.TryGetValue(point.MountPoint.MountType, out byName))
                    m_mountPoints[point.MountPoint.MountType] = byName = new Dictionary<string, MyProceduralMountPoint>();
                byName[point.MountPoint.MountName] = point;
            }
            parent?.RegisterRoom(this);
        }

        public MatrixI InvTransform => m_invTransform;

        public MatrixI Transform
        {
            get { return m_transform; }
            private set
            {
                var owner = Owner;
                owner?.RemoveRoom(this);
                m_transform = value;
                MatrixI.Invert(ref m_transform, out m_invTransform);
                using (m_part.LockSharedUsing())
                {
                    BoundingBox = MyUtilities.TransformBoundingBox(Part.BoundingBox, value);
                    ReservedSpace = MyUtilities.TransformBoundingBox(Part.ReservedSpace, value);
                    BoundingBoxBoth = MyUtilities.TransformBoundingBox(Part.BoundingBoxBoth, value);
                }
                m_intersectsWith.Clear();
                owner?.RegisterRoom(this);
            }
        }

        public MyProceduralMountPoint GetMountPoint(MyPartMount point)
        {
            Dictionary<string, MyProceduralMountPoint> byName;
            return !m_mountPoints.TryGetValue(point.MountType, out byName) ? null : byName?.GetValueOrDefault(point.MountName);
        }

        public BoundingBox BoundingBox { get; private set; }
        public BoundingBox BoundingBoxBoth { get; private set; }
        public BoundingBox ReservedSpace { get; private set; }

        public Vector3I GridToPrefab(Vector3I gridPos)
        {
            return Vector3I.Transform(gridPos, ref m_invTransform);
        }

        public Vector3I PrefabToGrid(Vector3I prefabPos)
        {
            return Vector3I.Transform(prefabPos, ref m_transform);
        }

        public bool Intersects(MyPartFromPrefab other, MatrixI otherTransform, MatrixI otherITransform, bool testOptional, bool testQuick = false)
        {
            return MyPartMetadata.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other, ref otherTransform, ref otherITransform, testOptional, testQuick);
        }

        private readonly Dictionary<int, byte> m_intersectsWith = new Dictionary<int, byte>();
        public bool Intersects(MyProceduralRoom other, bool testOptional, bool testQuick = false)
        {
            var presentMask = (byte)(testOptional ? 8 : 4);
            var mask = (byte)(testOptional ? 2 : 1);
            if (testQuick)
            {
                presentMask <<= 4;
                mask <<= 4;
            }
            byte intersect;
            if (other.Owner == Owner && m_intersectsWith.TryGetValue(other.RoomID, out intersect))
            {
                if ((intersect & presentMask) != 0)
                    return (intersect & mask) != 0;
            }
            else intersect = 0;
            var result = other.BoundingBoxBoth.Intersects(BoundingBoxBoth) &&
                (other.BoundingBox.Intersects(BoundingBox) || other.ReservedSpace.Intersects(ReservedSpace)) &&
                MyPartMetadata.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other.m_part, ref other.m_transform, ref other.m_invTransform, testOptional, testQuick);
            intersect |= presentMask;
            if (result)
                intersect |= mask;
            m_intersectsWith[other.RoomID] = intersect;
            other.m_intersectsWith[RoomID] = intersect;
            return result;
        }

        // present and intersecting and matching.
        public bool IntersectionCached(bool testOptional, bool testQuick = false)
        {
            var presentMask = (byte)(testOptional ? 8 : 4);
            var mask = (byte)(testOptional ? 2 : 1);
            if (testQuick)
            {
                presentMask <<= 4;
                mask <<= 4;
            }
            foreach (var kv in m_intersectsWith)
                if ((kv.Value & presentMask) != 0 && (kv.Value & mask) != 0 && Owner?.GetRoom(kv.Key) != null)
                    return true;
            return false;
        }

        public bool IsReserved(Vector3 pos, bool testShared = true, bool testOptional = true)
        {
            return ReservedSpace.Contains(pos) != ContainmentType.Disjoint && Part.IsReserved(pos, testShared, testOptional);
        }

        public bool CubeExists(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) != ContainmentType.Disjoint && Part.CubeExists(GridToPrefab(pos));
        }

        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) != ContainmentType.Disjoint ? Part.GetCubeAt(GridToPrefab(pos)) : null;
        }

        public IEnumerable<MyProceduralMountPoint> MountPoints => m_mountPoints?.Values.SelectMany(x => x.Values) ?? Enumerable.Empty<MyProceduralMountPoint>();
        
        public override string ToString()
        {
            return $"ProceduralRoom[{Part.Name} at {Transform.Translation}]";
        }
    }
}