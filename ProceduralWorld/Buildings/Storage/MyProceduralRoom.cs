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

        public MyObjectBuilder_ProceduralRoom GetObjectBuilder()
        {
            return new MyObjectBuilder_ProceduralRoom()
            {
                PrefabID = Part.Prefab.Id,
                Transform = Transform
            };
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
                owner?.AddRoom(this);
            }
        }

        public MyProceduralMountPoint GetMountPoint(MyPartMount point)
        {
            Dictionary<string, MyProceduralMountPoint> byName;
            return !m_mountPoints.TryGetValue(point.MountType, out byName) ? null : byName?.GetValueOrDefault(point.MountName);
        }

        public MyPartMountPointBlock GetMountPointBlockAt(Vector3I pos)
        {
            return Part.MountPointAt(GridToPrefab(pos));
        }

        public MyProceduralMountPoint GetMountPointAt(Vector3I pos)
        {
            MyPartMount backing = Part.MountPointAt(GridToPrefab(pos))?.Owner;
            return backing != null ? GetMountPoint(backing) : null;
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
        
        public bool Intersects(MyProceduralRoom other, bool testOptional, bool testQuick = false)
        {
            return other.BoundingBoxBoth.Intersects(BoundingBoxBoth) &&
                (other.BoundingBox.Intersects(BoundingBox) || other.ReservedSpace.Intersects(ReservedSpace)) &&
                MyPartMetadata.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other.m_part, ref other.m_transform, ref other.m_invTransform, testOptional, testQuick);
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