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
        private List<MyProceduralRoom> m_roomsSafeOrder;

        public MyProceduralConstruction()
        {
            m_maxID = 0;
            m_rooms = new Dictionary<long, MyProceduralRoom>();
            m_roomsSafeOrder = new List<MyProceduralRoom>();
        }

        public void Init(MyObjectBuilder_ProceduralConstruction ob)
        {
            m_rooms.Clear();
            m_roomsSafeOrder.Clear();
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
            m_roomsSafeOrder.Add(room);
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
            if (m_roomsSafeOrder[m_roomsSafeOrder.Count - 1] == room)
                m_roomsSafeOrder.RemoveAt(m_roomsSafeOrder.Count - 1);
            else
            {
                m_roomsSafeOrder.Remove(room);
                SessionCore.Log("Possibly unsafe removal of room not at end of safe list");
            }
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

        public bool Intersects(MyPart other, MatrixI otherTransform, MatrixI otherITransform, bool testOptional)
        {
            return m_rooms.Values.Any(test => test.Intersects(other, otherTransform, otherITransform, testOptional));
        }

        public bool Intersects(MyProceduralRoom room, bool testOptional)
        {
            return m_rooms.Values.Any(test => test != room && test.Intersects(room, testOptional));
        }

        public IEnumerable<MyProceduralRoom> Rooms => m_roomsSafeOrder;
    }

    public class MyProceduralRoom
    {
        public MyProceduralConstruction Owner { get; private set; }
        public long RoomID { get; private set; }
        public MyPart Part => m_part;
        private MyPart m_part;
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
            m_part = prefab;
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
            m_part = SessionCore.Instance.PartManager.LoadNullable(ob.PrefabID);
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
                BoundingBox = MyUtilities.TransformBoundingBox(Part.BoundingBox, value);
                ReservedSpace = MyUtilities.TransformBoundingBox(Part.ReservedSpace, value);
            }
        }

        public MyProceduralMountPoint GetMountPoint(MyPartMount point)
        {
            MyProceduralMountPoint mount;
            return m_mountPoints.TryGetValue(point, out mount) ? mount : null;
        }

        public BoundingBox BoundingBox { get; private set; }
        public BoundingBox ReservedSpace { get; private set; }

        public Vector3I GridToPrefab(Vector3I gridPos)
        {
            return Vector3I.Transform(gridPos, ref m_invTransform);
        }

        public Vector3I PrefabToGrid(Vector3I prefabPos)
        {
            return Vector3I.Transform(prefabPos, ref m_transform);
        }

        public bool Intersects(MyPart other, MatrixI otherTransform, MatrixI otherITransform, bool testOptional)
        {
            return MyPart.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other, ref otherTransform, ref otherITransform, testOptional);
        }

        public bool Intersects(MyProceduralRoom other, bool testOptional)
        {
            return MyPart.Intersects(ref m_part, ref m_transform, ref m_invTransform, ref other.m_part, ref other.m_transform, ref other.m_invTransform, testOptional);
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
            MountPoint = parent.Part.MountPoint(ob.TypeID, ob.InstanceID);
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
                    var mountPos = room.Part.MountPointAt(localPos);
                    if (mountPos != null)
                        return room.GetMountPoint(mountPos.Owner);
                }
                return null;
            }
        }
    }
}
