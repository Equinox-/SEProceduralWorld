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
    public class ProceduralRoom
    {
        private static readonly FastResourceLock MaxIDLock = new FastResourceLock();
        private static int MaxID = 0;

        public ProceduralConstruction Owner { get; private set; }
        internal int m_aabbProxyID;

        public int RoomID { get; private set; }
        public PartFromPrefab Part => m_part;
        private PartFromPrefab m_part;
        private MatrixI m_transform, m_invTransform;
        private Dictionary<string, Dictionary<string, ProceduralMountPoint>> m_mountPoints;

        public event Action AddedToConstruction, RemovedFromConstruction;

        public ProceduralRoom()
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

        internal void TakeOwnership(ProceduralConstruction parent)
        {
            Orphan();
            if (parent == null) return;
            Owner = parent;
            AddedToConstruction?.Invoke();
        }

        public void Init(MatrixI transform, PartFromPrefab prefab)
        {
            m_part = prefab;
            Transform = transform;
            m_mountPoints = new Dictionary<string, Dictionary<string, ProceduralMountPoint>>();
            using (m_part.LockSharedUsing())
                foreach (var mount in prefab.MountPoints)
                {
                    var point = new ProceduralMountPoint();
                    point.Init(mount, this);
                    Dictionary<string, ProceduralMountPoint> byName;
                    if (!m_mountPoints.TryGetValue(point.MountPoint.MountType, out byName))
                        m_mountPoints[point.MountPoint.MountType] = byName = new Dictionary<string, ProceduralMountPoint>();
                    byName[point.MountPoint.MountName] = point;
                }
        }

        public Ob_ProceduralRoom GetObjectBuilder()
        {
            return new Ob_ProceduralRoom()
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
                    BoundingBox = Utilities.TransformBoundingBox(Part.BoundingBox, value);
                    ReservedSpace = Utilities.TransformBoundingBox(Part.ReservedSpace, value);
                    BoundingBoxBoth = Utilities.TransformBoundingBox(Part.BoundingBoxBoth, value);
                }
                owner?.AddRoom(this);
            }
        }

        public ProceduralMountPoint GetMountPoint(PartMount point)
        {
            Dictionary<string, ProceduralMountPoint> byName;
            return !m_mountPoints.TryGetValue(point.MountType, out byName) ? null : byName?.GetValueOrDefault(point.MountName);
        }

        public PartMountPointBlock GetMountPointBlockAt(Vector3I pos)
        {
            return Part.MountPointAt(GridToPrefab(pos));
        }

        public ProceduralMountPoint GetMountPointAt(Vector3I pos)
        {
            PartMount backing = Part.MountPointAt(GridToPrefab(pos))?.Owner;
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

        public bool Intersects(PartFromPrefab other, MatrixI otherTransform, MatrixI otherITransform, bool testOptional, bool testQuick = false)
        {
            return PartMetadata.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other, ref otherTransform, ref otherITransform, testOptional, testQuick);
        }
        
        public bool Intersects(ProceduralRoom other, bool testOptional, bool testQuick = false)
        {
            return other.BoundingBoxBoth.Intersects(BoundingBoxBoth) &&
                (other.BoundingBox.Intersects(BoundingBox) || other.ReservedSpace.Intersects(ReservedSpace)) &&
                PartMetadata.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other.m_part, ref other.m_transform, ref other.m_invTransform, testOptional, testQuick);
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

        public IEnumerable<ProceduralMountPoint> MountPoints => m_mountPoints?.Values.SelectMany(x => x.Values) ?? Enumerable.Empty<ProceduralMountPoint>();
        
        public override string ToString()
        {
            return $"ProceduralRoom[{Part.Name} at {Transform.Translation}]";
        }
    }
}