using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;
using System.Diagnostics;
using Equinox.ProceduralWorld.Buildings.Creation.Remap;
using Equinox.Utils.Logging;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class RoomRemapper
    {
        private class RemapCollection
        {
            private readonly List<IGridRemap> m_remap = new List<IGridRemap>();
            private readonly Dictionary<Type, IGridRemap> m_remapByType = new Dictionary<Type, IGridRemap>();

            public void Add(IGridRemap remap)
            {
                m_remap.Add(remap);
                m_remapByType[remap.GetType()] = remap;
            }

            public T Remap<T>() where T : IGridRemap
            {
                IGridRemap val;
                if (!m_remapByType.TryGetValue(typeof(T), out val))
                    return null;
                return val as T;
            }

            public void RemapAndReset(ICollection<MyObjectBuilder_CubeGrid> grids)
            {
                foreach (var op in m_remap)
                    op.RemapAndReset(grids);
            }
        }

        private readonly RemapCollection m_allPre = new RemapCollection();
        private readonly RemapCollection m_primary = new RemapCollection();
        private readonly RemapCollection m_auxiliary = new RemapCollection();
        private readonly RemapCollection m_allPost = new RemapCollection();

        public bool DebugRoomColors = true;

        public readonly ILogging Logger;
        public RoomRemapper(ILoggingBase root)
        {
            Logger = root.CreateProxy(GetType().Name);
            m_allPre.Add(new GridRemap_Names(root));
            m_allPre.Add(new Coloring(root));
            m_allPre.Add(new GridRemap_Ownership(root));
            m_primary.Add(new GridRemap_LocalTransform(root));
            m_auxiliary.Add(new GridRemap_WorldTransform(root));
            DebugRoomColors = Settings.DebugDrawRoomColors;
        }

        public T Remap<T>() where T : IGridRemap
        {
            var result = m_allPre.Remap<T>() ?? m_primary.Remap<T>() ?? m_auxiliary.Remap<T>() ?? m_allPost.Remap<T>();
            if (result == null)
                throw new KeyNotFoundException($"Failed to find remapper {typeof(T)}");
            return result;
        }

        public void Remap(ProceduralRoom room, ConstructionCopy dest)
        {
            if (dest.PrimaryGrid.GridSizeEnum != room.Part.PrimaryCubeSize)
                throw new ArgumentException("Primary grid cube size and room's primary cube size differ");
            // Setup remap parameters
            {
                var localTransform = Remap<GridRemap_LocalTransform>();
                localTransform.LocalTransform = room.Transform;
            }

            {
                var naming = Remap<GridRemap_Names>();
                naming.PrefixFor(GridRemap_Names.RemapType.All, room.GetName() + " ");
            }

            {
                var coloring = Remap<Coloring>();
                coloring.OverrideColor = DebugRoomColors ? (SerializableVector3?)Utilities.NextColor.ColorToHSV() : null;
                coloring.HueRotation = room.Owner.Seed.Faction.HueRotation;
                coloring.SaturationModifier = room.Owner.Seed.Faction.SaturationModifier;
                coloring.ValueModifier = room.Owner.Seed.Faction.ValueModifier;
            }

            {
                var ownership = Remap<GridRemap_Ownership>();
                var faction = room.Owner.Seed.Faction.GetOrCreateFaction();
                ownership.OwnerID = faction?.FounderId ?? 0;
                ownership.ShareMode = MyOwnershipShareModeEnum.Faction;
                ownership.UpgradeShareModeOnly = true;
            }

            var worldTransform = Remap<GridRemap_WorldTransform>();
            {
                var roomTransformScaled = room.Transform.GetFloatMatrix();
                roomTransformScaled.Translation *= MyDefinitionManager.Static.GetCubeSize(dest.PrimaryGrid.GridSizeEnum);

                var prefabPrimaryGridNewWorldMatrix = Matrix.Multiply(roomTransformScaled,
                    dest.PrimaryGrid.PositionAndOrientation?.GetMatrix() ?? MatrixD.Identity);
                var prefabPrimaryGridOldWorldMatrix = room.Part.PrimaryGrid.PositionAndOrientation?.GetMatrix() ??
                                                      MatrixD.Identity;


                var prefabOldToNew = Matrix.Multiply(Matrix.Invert(prefabPrimaryGridOldWorldMatrix),
                    prefabPrimaryGridNewWorldMatrix);

                worldTransform.WorldTransform = prefabOldToNew;
                worldTransform.WorldLinearVelocity = dest.PrimaryGrid.LinearVelocity;
            }

            // Grab OB copies
            var timer = new Stopwatch();
            timer.Restart();
            var roomGrid = CloneUtilities.CloneFast(room.Part.PrimaryGrid);
            var otherGrids = room.Part.Prefab.CubeGrids.Where(x => x != room.Part.PrimaryGrid).Select(CloneUtilities.CloneFast).ToList();
            var allGrids = new List<MyObjectBuilder_CubeGrid>(otherGrids) { roomGrid };
            Logger.Debug("Cloned {0} grids in {1}", allGrids.Count, timer.Elapsed);


            // Remap entity IDs
            timer.Restart();
            MyAPIGateway.Entities.RemapObjectBuilderCollection(allGrids);
            // If we have a primary ID copy it now.
            if (dest.PrimaryGrid.EntityId != 0)
            {
                var constRemapID = new ConstantEntityRemap(new Dictionary<long, long> { [roomGrid.EntityId] = dest.PrimaryGrid.EntityId });
                // Anything referring to the root grid's entity ID needs to be changed to the old grid.
                foreach (var c in allGrids)
                    c.Remap(constRemapID);
            }
            else // otherwise, skip
                dest.PrimaryGrid.EntityId = roomGrid.EntityId;
            Logger.Debug("Remapped {0} grid IDs in {1}", allGrids.Count, timer.Elapsed);

            // Apply remap operators
            m_allPre.RemapAndReset(allGrids);
            m_primary.RemapAndReset(new[] { roomGrid });
            m_auxiliary.RemapAndReset(otherGrids);
            m_allPost.RemapAndReset(allGrids);

            // Merge data into primary grid from room grid
            dest.PrimaryGrid.CubeBlocks.Capacity += roomGrid.CubeBlocks.Count;
            dest.PrimaryGrid.CubeBlocks.AddRange(roomGrid.CubeBlocks);

            dest.PrimaryGrid.BlockGroups.Capacity += roomGrid.BlockGroups.Count;
            dest.PrimaryGrid.BlockGroups.AddRange(roomGrid.BlockGroups);

            // Seems suboptimal?  Can we transform this and only invalidate ones on a room border?
            dest.PrimaryGrid.ConveyorLines.Clear();

            // Not even going to try.
            dest.PrimaryGrid.OxygenAmount = null;
            dest.PrimaryGrid.Skeleton = null;

            // Add aux grids
            dest.AuxGrids.AddRange(otherGrids);

            dest.BoundingBox = BoundingBoxD.CreateMerged(dest.BoundingBox, Utilities.TransformBoundingBox((BoundingBoxD) room.BoundingBoxBoth, worldTransform.WorldTransform));
        }
    }
}