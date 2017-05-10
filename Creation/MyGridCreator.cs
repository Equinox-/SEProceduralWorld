using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Construction;
using ProcBuild.Generation;
using ProcBuild.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ProcBuild.Creation
{
    public static partial class MyGridCreator
    {
        public static MyObjectBuilder_CubeGrid SpawnRoomAt(MyProceduralRoom room, MatrixD spawnLocation)
        {
            return SpawnRoomInternal(null, room, spawnLocation);
        }

        public static void AppendRoom(MyObjectBuilder_CubeGrid dest, MyProceduralRoom room)
        {
            SpawnRoomInternal(dest, room, default(MatrixD));
        }

        private static IMyCubeGrid SpawnRoomInternal(IMyCubeGrid procGrid, MyProceduralRoom room, MatrixD spawnLocation)
        {
            var rootGridCopy = BlockTransformations.CloneGrid(room.Part.PrimaryGrid);
            var otherGridsCopy = room.Part.Prefab.CubeGrids.Where(x => x != room.Part.PrimaryGrid).Select(BlockTransformations.CloneGrid).ToArray();
            var allGridsCopy = new List<MyObjectBuilder_CubeGrid>(otherGridsCopy) { rootGridCopy };
            MyAPIGateway.Entities.RemapObjectBuilderCollection(allGridsCopy);

            if (procGrid != null)
            {
                var constRemapID = new MyConstantEntityRemap(new Dictionary<long, long> { [rootGridCopy.EntityId] = procGrid.EntityId });
                // Anything referring to the root grid's entity ID needs to be changed to the old grid.
                foreach (var c in allGridsCopy)
                    c.Remap(constRemapID);
            }

            // Calculate World to World transform for grids in this group.
            // actual pos = procGrid.WorldMatrix * room.Transform * Invert(root.WorldMatrix) * vpos
            var roomTransformScaled = room.Transform.GetFloatMatrix();
            if (procGrid != null)
                roomTransformScaled.Translation *= procGrid.GridSize;
            else
                roomTransformScaled.Translation *= MyDefinitionManager.Static.GetCubeSize(room.Part.PrimaryGrid.GridSizeEnum);
            var prefabToWorld = MatrixD.Multiply(MatrixD.Invert(rootGridCopy.PositionAndOrientation?.AsMatrixD() ?? MatrixD.Identity), roomTransformScaled);
            prefabToWorld = MatrixD.Multiply(prefabToWorld, procGrid?.WorldMatrix ?? spawnLocation);

            // Fix all world transforms
            foreach (var c in otherGridsCopy)
            {
                if (c.PositionAndOrientation.HasValue)
                    c.PositionAndOrientation = new MyPositionAndOrientation(MatrixD.Multiply(c.PositionAndOrientation.Value.GetMatrix(), prefabToWorld));
                c.AngularVelocity = Vector3.TransformNormal(c.AngularVelocity, prefabToWorld);
                c.LinearVelocity = Vector3.TransformNormal(c.LinearVelocity, prefabToWorld);
            }

            // Make it hella colorful 
            {
                SerializableVector3 color = colors[(colorID++) % colors.Length].ColorToHSV();
                foreach (var c in allGridsCopy)
                    foreach (var b in c.CubeBlocks)
                        b.ColorMaskHSV = color;
            }

            var moduleName = room.GetName();

            // Fix all block locations of root grid.
            var blockLocationMap = new Dictionary<Vector3I, Vector3I>();
            foreach (var b in rootGridCopy.CubeBlocks)
            {
                var ipos = b.Min;
                BlockTransformations.ApplyTransformation(b, room.Transform);
                blockLocationMap[ipos] = b.Min;
            }
            // Fix all block groups
            if (rootGridCopy.BlockGroups != null)
                foreach (var g in rootGridCopy.BlockGroups)
                    for (var i = 0; i < g.Blocks.Count; i++)
                    {
                        Vector3I opos;
                        if (blockLocationMap.TryGetValue(g.Blocks[i], out opos))
                            g.Blocks[i] = opos;
                    }

            // Rename blocks
            foreach (var c in allGridsCopy)
            {
                foreach (var b in c.CubeBlocks)
                {
                    var termB = b as MyObjectBuilder_TerminalBlock;
                    if (termB?.CustomName != null)
                        termB.CustomName = moduleName + " " + termB.CustomName;

                    MyObjectBuilder_Toolbar toolbar = null;
                    toolbar = (b as MyObjectBuilder_ButtonPanel)?.Toolbar;
                    toolbar = toolbar ?? (b as MyObjectBuilder_Cockpit)?.Toolbar;
                    toolbar = toolbar ?? (b as MyObjectBuilder_ShipController)?.Toolbar;
                    toolbar = toolbar ?? (b as MyObjectBuilder_TimerBlock)?.Toolbar;
                    if (toolbar != null)
                        foreach (var s in toolbar.Slots)
                        {
                            var termGroup = s.Data as MyObjectBuilder_ToolbarItemTerminalGroup;
                            if (termGroup?.GroupName != null)
                                termGroup.GroupName = moduleName + " " + termGroup.GroupName;
                        }
                }

                // Rename block groups
                foreach (var g in c.BlockGroups)
                {
                    if (g.Name != null)
                        g.Name = moduleName + " " + g.Name;
                }
            }
            // Rename aux grids
            foreach (var c in otherGridsCopy)
            {
                if (c.DisplayName != null)
                    c.DisplayName = moduleName + " " + c.DisplayName;
                if (c.Name != null)
                    c.Name = moduleName + " " + c.Name;
            }

            // Delete all conveyor lines
            foreach (var c in allGridsCopy)
                c.ConveyorLines = new List<MyObjectBuilder_ConveyorLine>();

            // Add blocks from dummy grid to main grid
            if (procGrid != null)
            {
                foreach (var c in rootGridCopy.CubeBlocks)
                    if (procGrid.AddBlock(c, false) == null)
                        SessionCore.Log("Failed to add a block at {0}", c.Min);
                //            SAD DAY IN NEVERLAND
                //                foreach (var g in rootGridCopy.BlockGroups)
                //                    MyGrid
            }
            else
            {
                rootGridCopy.AngularVelocity = Vector3.Zero;
                rootGridCopy.LinearVelocity = Vector3.Zero;
                rootGridCopy.IsStatic = true;
                rootGridCopy.XMirroxPlane = null;
                rootGridCopy.YMirroxPlane = null;
                rootGridCopy.ZMirroxPlane = null;
                rootGridCopy.PersistentFlags = MyPersistentEntityFlags2.InScene | MyPersistentEntityFlags2.CastShadows | MyPersistentEntityFlags2.Enabled;
                rootGridCopy.DisplayName = "GenGrid_" + room.Owner.GetHashCode();
                rootGridCopy.PositionAndOrientation = new MyPositionAndOrientation(spawnLocation);
                procGrid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(rootGridCopy) as IMyCubeGrid;
            }
            // Add aux grids!
            foreach (var other in otherGridsCopy)
            {
                //                other.IsStatic = true;
                other.PersistentFlags = MyPersistentEntityFlags2.InScene | MyPersistentEntityFlags2.CastShadows | MyPersistentEntityFlags2.Enabled;
                MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(other);
            }
            return procGrid;
        }
    }
}
