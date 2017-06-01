﻿using System;
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

namespace Equinox.ProceduralWorld.Buildings.Creation
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
                IMyGridRemap val;
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

        public bool DebugRoomColors = false;

        public MyRoomRemapper()
        {
            m_allPre.Add(new MyGridRemap_Names());
            m_allPre.Add(new MyGridRemap_Coloring());
            m_allPre.Add(new MyGridRemap_Ownership());
            m_primary.Add(new MyGridRemap_LocalTransform());
            m_auxiliary.Add(new MyGridRemap_WorldTransform());
            DebugRoomColors = Settings.Instance.DebugDrawRoomColors;
        }

        public T Remap<T>() where T : class, IMyGridRemap
        {
            var result = m_allPre.Remap<T>() ?? m_primary.Remap<T>() ?? m_auxiliary.Remap<T>() ?? m_allPost.Remap<T>();
            if (result == null)
                SessionCore.Log("Failed to find remapper {0}", typeof(T));
            return result;
        }

        public void Remap(MyProceduralRoom room, MyConstructionCopy dest)
        {
            if (dest.PrimaryGrid.GridSizeEnum != room.Part.PrimaryCubeSize)
                throw new ArgumentException("Primary grid cube size and room's primary cube size differ");
            // Setup remap parameters
            {
                var localTransform = Remap<MyGridRemap_LocalTransform>();
                localTransform.LocalTransform = room.Transform;
            }

            {
                var naming = Remap<MyGridRemap_Names>();
                naming.PrefixFor(MyGridRemap_Names.RemapType.All, room.GetName() + " ");
            }

            {
                var coloring = Remap<MyGridRemap_Coloring>();
                coloring.OverrideColor = DebugRoomColors ? (SerializableVector3?)MyUtilities.NextColor.ColorToHSV() : null;
                coloring.HueRotation = room.Owner.Seed.Faction.HueRotation;
                coloring.SaturationModifier = room.Owner.Seed.Faction.SaturationModifier;
                coloring.ValueModifier = room.Owner.Seed.Faction.ValueModifier;
            }

            {
                var ownership = Remap<MyGridRemap_Ownership>();
                var faction = room.Owner.Seed.Faction.GetOrCreateFaction();
                ownership.OwnerID = faction?.FounderId ?? 0;
                ownership.ShareMode = MyOwnershipShareModeEnum.Faction;
                ownership.UpgradeShareModeOnly = true;
            }

            {
                var worldTransform = Remap<MyGridRemap_WorldTransform>();

                var roomTransformScaled = room.Transform.GetFloatMatrix();
                roomTransformScaled.Translation *= MyDefinitionManager.Static.GetCubeSize(dest.PrimaryGrid.GridSizeEnum);

                var prefabToWorld = MatrixD.Multiply(MatrixD.Invert(room.Part.PrimaryGrid.PositionAndOrientation?.AsMatrixD() ?? MatrixD.Identity), roomTransformScaled);
                prefabToWorld = MatrixD.Multiply(prefabToWorld, dest.PrimaryGrid.PositionAndOrientation?.GetMatrix() ?? MatrixD.Identity);

                worldTransform.WorldTransform = prefabToWorld;
                worldTransform.WorldLinearVelocity = dest.PrimaryGrid.LinearVelocity;
            }

            // Grab OB copies
            var begin = new Stopwatch();
            begin.Restart();
            var roomGrid = MyCloneUtilities.CloneFast(room.Part.PrimaryGrid);
            var otherGrids = room.Part.Prefab.CubeGrids.Where(x => x != room.Part.PrimaryGrid).Select(MyCloneUtilities.CloneFast).ToList();
            var allGrids = new List<MyObjectBuilder_CubeGrid>(otherGrids) { roomGrid };
            if (Settings.Instance.DebugRoomRemapProfiling)
                SessionCore.Log("Cloned {0} grids in {1}", allGrids.Count, begin.Elapsed);


            // Remap entity IDs
            begin.Restart();
            MyAPIGateway.Entities.RemapObjectBuilderCollection(allGrids);
            // If we have a primary ID copy it now.
            if (dest.PrimaryGrid.EntityId != 0)
            {
                var constRemapID = new MyConstantEntityRemap(new Dictionary<long, long> { [roomGrid.EntityId] = dest.PrimaryGrid.EntityId });
                // Anything referring to the root grid's entity ID needs to be changed to the old grid.
                foreach (var c in allGrids)
                    c.Remap(constRemapID);
            }
            else // otherwise, skip
                dest.PrimaryGrid.EntityId = roomGrid.EntityId;
            if (Settings.Instance.DebugRoomRemapProfiling)
                SessionCore.Log("Remapped {0} grid IDs in {1}", allGrids.Count, begin.Elapsed);

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
        }
    }
}