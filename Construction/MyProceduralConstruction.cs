using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Construction
{
    public class MyProceduralConstruction
    {
        private Dictionary<long, MyProceduralRoom> m_rooms;

        public MyProceduralConstruction()
        {
            m_maxID = 0;
        }

        public void Init(MyObjectBuilder_ProceduralConstruction ob)
        {
            m_rooms.Clear();
            m_maxID = 0;
            foreach (var room in ob.Room)
                new MyProceduralRoom().Init(room, this);
        }

        internal void RegisterRoom(MyProceduralRoom room)
        {
            if (m_rooms.ContainsKey(room.RoomID))
                throw new ArgumentException("Room ID already used");
            m_maxID = Math.Max(m_maxID, room.RoomID);
            m_rooms[room.RoomID] = room;
        }

        public MyProceduralRoom GenerateRoom(MatrixI transform, MyPart prefab)
        {
            var tmp = new MyProceduralRoom();
            tmp.Init(this, transform, prefab);
            return tmp;
        }

        public void RemoveRoom(MyProceduralRoom room)
        {
            m_rooms.Remove(room.RoomID);
            room.Orphan();
        }

        public void AddCachedRoom(MyProceduralRoom room)
        {
            room.TakeOwnership(this);
        }

        private long m_maxID;
        internal long AcquireID()
        {
            m_maxID++;
            return m_maxID;
        }

        public MyProceduralRoom GetRoomAt(Vector3I pos)
        {
            return m_rooms.Values.FirstOrDefault(room => room.CubeExists(pos));
        }

        public bool CubeExists(Vector3I pos)
        {
            return m_rooms.Values.Any(room => room.CubeExists(pos));
        }

        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            return m_rooms.Values.Select(room => room.GetCubeAt(pos)).FirstOrDefault(ob => ob != null);
        }

        public bool Intersects(MyProceduralRoom room)
        {
            return m_rooms.Values.Where(test => test.BoundingBox.Intersects(room.BoundingBox)).Any(test => room.OccupiedCubes.Any(test.CubeExists));
        }

        public IEnumerable<MyProceduralRoom> Rooms => m_rooms.Values;
    }

    public class MyProceduralRoom
    {
        public MyProceduralConstruction Owner { get; private set; }
        public long RoomID { get; private set; }
        public MyPart Prefab { get; private set; }
        private MatrixI m_transform, m_invTransform;
        private Dictionary<MyPartMount, MyProceduralMountPoint> m_mountPoints;

        public MyProceduralRoom()
        {
            Owner = null;
            RoomID = -1;
        }

        internal void Orphan()
        {
            RoomID = -1;
            Owner = null;
        }

        internal void TakeOwnership(MyProceduralConstruction parent)
        {
            Orphan();
            if (parent == null) return;
            Owner = parent;
            RoomID = parent.AcquireID();
            parent.RegisterRoom(this);
        }

        internal void Init(MyProceduralConstruction parent, MatrixI transform, MyPart prefab)
        {
            Owner = parent;
            RoomID = parent.AcquireID();
            Prefab = prefab;
            Transform = transform;
            m_mountPoints = new Dictionary<MyPartMount, MyProceduralMountPoint>();
            foreach (var mount in prefab.MountPoints)
            {
                var point = new MyProceduralMountPoint();
                point.Init(mount, this);
                m_mountPoints[mount] = point;
            }
            parent?.RegisterRoom(this);
        }

        public void Init(MyObjectBuilder_ProceduralRoom ob, MyProceduralConstruction parent)
        {
            Owner = parent;
            RoomID = parent != null ? ob.RoomID : -1;
            Prefab = SessionCore.Instance.PartManager.LoadNullable(ob.PrefabID);
            Transform = ob.Transform;
            m_mountPoints = new Dictionary<MyPartMount, MyProceduralMountPoint>(ob.MountPoints.Length);
            foreach (var mount in ob.MountPoints)
            {
                var point = new MyProceduralMountPoint();
                point.Init(mount, this);
                m_mountPoints[point.MountPoint] = point;
            }
            parent?.RegisterRoom(this);
        }

        public MatrixI Transform
        {
            get { return m_transform; }
            set
            {
                m_transform = value;
                MatrixI.Invert(ref m_transform, out m_invTransform);
                BoundingBox = MyUtilities.TransformBoundingBox(Prefab.m_boundingBox, value);
            }
        }

        public MyProceduralMountPoint GetMountPoint(MyPartMount point)
        {
            MyProceduralMountPoint mount;
            return m_mountPoints.TryGetValue(point, out mount) ? mount : null;
        }

        public BoundingBox BoundingBox { get; private set; }

        public Vector3I GridToPrefab(Vector3I gridPos)
        {
            return Vector3I.Transform(gridPos, ref m_invTransform);
        }

        public Vector3I PrefabToGrid(Vector3I prefabPos)
        {
            return Vector3I.Transform(prefabPos, ref m_transform);
        }

        public bool CubeExists(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) == ContainmentType.Contains && Prefab.CubeExists(GridToPrefab(pos));
        }

        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            return BoundingBox.Contains((Vector3)pos) == ContainmentType.Contains ? Prefab.GetCubeAt(GridToPrefab(pos)) : null;
        }

        public IEnumerable<Vector3I> OccupiedCubes => Prefab.Occupied.Select(PrefabToGrid);

        public IEnumerable<MyProceduralMountPoint> MountPoints => m_mountPoints.Values;
    }

    public class MyProceduralMountPoint
    {
        public MyProceduralRoom Owner { get; private set; }
        public MyPartMount MountPoint { get; private set; }

        public MyProceduralMountPoint()
        {
            Owner = null;
            MountPoint = null;
        }

        internal void Init(MyPartMount mount, MyProceduralRoom parent)
        {
            Owner = parent;
            MountPoint = mount;
        }

        public void Init(MyObjectBuilder_ProceduralMountPoint ob, MyProceduralRoom parent)
        {
            Owner = parent;
            MountPoint = parent.Prefab.MountPoint(ob.TypeID, ob.InstanceID);
        }

        public MyProceduralMountPoint AttachedTo
        {
            get
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var block in MountPoint.m_blocks.Values.SelectMany(x => x))
                {
                    var gridPos = Owner.PrefabToGrid(block.MountLocation);
                    var room = Owner.Owner.GetRoomAt(gridPos);
                    if (room == null) continue;
                    var localPos = room.GridToPrefab(gridPos);
                    var mountPos = room.Prefab.MountPointAt(localPos);
                    if (mountPos != null)
                        return room.GetMountPoint(mountPos.Owner);
                }
                return null;
            }
        }
    }
}
