using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.FileSystem;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Serialization;
using VRageMath;

namespace ProcBuild
{
    internal class MyGridBuilder
    {
        public struct RoomInstance
        {
            public readonly MyPart m_part;
            private MatrixI m_transform, m_invTransform;
            public readonly HashSet<MyPartMount> m_freeMounts;

            public RoomInstance(MyPart part, MatrixI transform)
            {
                this.m_part = part;
                this.m_transform = transform;
                MatrixI.Invert(ref m_transform, out m_invTransform);
                this.m_freeMounts = new HashSet<MyPartMount>(this.m_part.MountPoints);
                this.BoundingBox = MyUtilities.TransformBoundingBox(part.m_boundingBox, transform);
            }

            public BoundingBox BoundingBox { get; }

            public IEnumerable<Vector3I> Occupied
            {
                get
                {
                    var transform = m_transform;
                    return m_part.Occupied.Select(x => Vector3I.Transform(x, ref transform));
                }
            }

            public bool CubeExists(Vector3I pos)
            {
                return m_part.CubeExists(Vector3I.Transform(pos, ref m_invTransform));
            }

            public MatrixI Transform => m_transform;
        }

        private readonly List<RoomInstance> m_rooms;

        public MyGridBuilder()
        {
            this.m_rooms = new List<RoomInstance>();
        }

        public IEnumerable<RoomInstance> Rooms => m_rooms;

        public void Add(MyPart part, MatrixI transform)
        {
            Add(new RoomInstance(part, transform));
        }

        public void Add(RoomInstance instance)
        {
            m_rooms.Add(instance);
        }

        public bool Intersects(RoomInstance instance)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var pair in m_rooms)
                if (pair.BoundingBox.Intersects(instance.BoundingBox) && pair.Occupied.Any(x => instance.CubeExists(x)))
                    return true;
            return false;
        }

        public bool Intersects(MyPart part, MatrixI transform)
        {
            return Intersects(new RoomInstance(part, transform));
        }

        public IEnumerable<MyObjectBuilder_CubeBlock> CubeBlocks
        {
            get
            {
                var i = 0;
                Color[] cols = { Color.Brown, Color.AliceBlue, Color.HotPink, Color.Aqua, Color.Lime, Color.Chocolate, Color.DarkSalmon };
                foreach (var room in m_rooms)
                {
                    var col = cols[i++ % cols.Length];
                    foreach (var block in room.m_part.m_grid.CubeBlocks)
                    {
                        var cb = BlockTransformations.CopyAndTransform(block, room.Transform);
                        cb.ColorMaskHSV = col.ColorToHSV();
                        yield return cb;
                        //grid.CubeBlocks.Add(cb);
                    }
                }
            }
        }

        public MyObjectBuilder_CubeGrid CubeGrid
        {
            get
            {
                var grid = new MyObjectBuilder_CubeGrid
                {
                    CubeBlocks = new List<MyObjectBuilder_CubeBlock>(),
                    AngularVelocity = Vector3.Zero,
                    LinearVelocity = Vector3.Zero,
                    GridSizeEnum = MyCubeSize.Large,
                    IsStatic = true,
                    XMirroxPlane = null,
                    YMirroxPlane = null,
                    ZMirroxPlane = null,
                    PersistentFlags = MyPersistentEntityFlags2.InScene,
                    DisplayName = "GenGrid_" + GetHashCode(),
                    EntityId = 0
                };
                grid.CubeBlocks.Add(CubeBlocks.First());
                return grid;
            }
        }
    }
}
