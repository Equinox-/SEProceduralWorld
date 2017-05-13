using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Generation;
using ProcBuild.Storage;
using ProcBuild.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace ProcBuild.Creation
{
    public class MyRoomRemapper
    {
        private class RemapCollection
        {
            private readonly List<IMyGridRemap> m_remap = new List<IMyGridRemap>();
            private readonly Dictionary<Type, IMyGridRemap> m_remapByType = new Dictionary<Type, IMyGridRemap>();

            public void Add(IMyGridRemap remap)
            {
                m_remap.Add(remap);
                m_remapByType[remap.GetType()] = remap;
            }

            public T Remap<T>() where T : class, IMyGridRemap
            {
                return m_remapByType[typeof(T)] as T;
            }

            public void RemapAndReset(ICollection<MyObjectBuilder_CubeGrid> grids)
            {
                foreach (var op in m_remap)
                    op.RemapAndReset(grids);
            }
        }

        private readonly RemapCollection m_all = new RemapCollection();
        private readonly RemapCollection m_primary = new RemapCollection();
        private readonly RemapCollection m_auxiliary = new RemapCollection();

        public MyRoomRemapper()
        {
            m_all.Add(new MyGridRemap_Names());
            m_all.Add(new MyGridRemap_Coloring());
            m_primary.Add(new MyGridRemap_LocalTransform());
            m_auxiliary.Add(new MyGridRemap_WorldTransform());
        }

        public void Remap(MyProceduralRoom room, MyConstructionCopy dest)
        {
            if (dest.m_primaryGrid.GridSizeEnum != room.Part.PrimaryCubeSize)
                throw new ArgumentException("Primary grid cube size and room's primary cube size differ");

            // Setup remap parameters
            {
                var localTransform = m_primary.Remap<MyGridRemap_LocalTransform>();
                localTransform.LocalTransform = room.Transform;
            }

            {
                var naming = m_all.Remap<MyGridRemap_Names>();
                naming.PrefixFor(MyGridRemap_Names.RemapType.All, room.GetName() + " ");
            }

            {
                var coloring = m_all.Remap<MyGridRemap_Coloring>();
                coloring.OverrideColor = MyUtilities.NextColor.ColorToHSV();
            }

            {
                var worldTransform = m_auxiliary.Remap<MyGridRemap_WorldTransform>();

                var roomTransformScaled = room.Transform.GetFloatMatrix();
                roomTransformScaled.Translation *= MyDefinitionManager.Static.GetCubeSize(dest.m_primaryGrid.GridSizeEnum);

                var prefabToWorld = MatrixD.Multiply(MatrixD.Invert(room.Part.PrimaryGrid.PositionAndOrientation?.AsMatrixD() ?? MatrixD.Identity), roomTransformScaled);
                prefabToWorld = MatrixD.Multiply(prefabToWorld, dest.m_primaryGrid.PositionAndOrientation?.GetMatrix() ?? MatrixD.Identity);

                worldTransform.WorldTransform = prefabToWorld;
                worldTransform.WorldLinearVelocity = dest.m_primaryGrid.LinearVelocity;
            }

            // Grab OB copies
            var roomGrid = (MyObjectBuilder_CubeGrid)room.Part.PrimaryGrid.Clone();
            var otherGrids = room.Part.Prefab.CubeGrids.Where(x => x != room.Part.PrimaryGrid).Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToList();
            var allGrids = new List<MyObjectBuilder_CubeGrid>(otherGrids) { roomGrid };

            // Remap entity IDs
            MyAPIGateway.Entities.RemapObjectBuilderCollection(allGrids);
            // If we have a primary ID copy it now.
            if (dest.m_primaryGrid.EntityId != 0)
            {
                var constRemapID = new MyConstantEntityRemap(new Dictionary<long, long> { [roomGrid.EntityId] = dest.m_primaryGrid.EntityId });
                // Anything referring to the root grid's entity ID needs to be changed to the old grid.
                foreach (var c in allGrids)
                    c.Remap(constRemapID);
            }
            else // otherwise, skip
                dest.m_primaryGrid.EntityId = roomGrid.EntityId;

            // Apply remap operators
            m_all.RemapAndReset(allGrids);
            m_primary.RemapAndReset(new[] { roomGrid });
            m_auxiliary.RemapAndReset(otherGrids);

            // Merge data into primary grid from room grid
            dest.m_primaryGrid.CubeBlocks.Capacity += roomGrid.CubeBlocks.Count;
            dest.m_primaryGrid.CubeBlocks.AddRange(roomGrid.CubeBlocks);

            dest.m_primaryGrid.BlockGroups.Capacity += roomGrid.BlockGroups.Count;
            dest.m_primaryGrid.BlockGroups.AddRange(roomGrid.BlockGroups);

            // Seems suboptimal?  Can we transform this and only invalidate ones on a room border?
            dest.m_primaryGrid.ConveyorLines.Clear();

            // Not even going to try.
            dest.m_primaryGrid.OxygenAmount = null;
            dest.m_primaryGrid.Skeleton = null;

            // Add aux grids
            dest.m_auxGrids.AddRange(otherGrids);
        }
    }
}