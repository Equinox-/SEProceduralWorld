using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Exporter
{
    public class MyDesignTool
    {
        public const string DELEGATED_TAG = "del";
        public const string MOUNT_DELEGATED = MyPartMetadata.MOUNT_PREFIX + " " + DELEGATED_TAG;
        public const string RESERVED_SPACE_DELEGATED = MyPartMetadata.RESERVED_SPACE_PREFIX + " " + DELEGATED_TAG;

        private static bool ApplyDelegate(MyObjectBuilder_CubeGrid grid, MyObjectBuilder_CubeBlock source, string srcName, MyObjectBuilder_CubeBlock dest, Base6Directions.Direction destDir)
        {
            if (srcName.StartsWithICase(MOUNT_DELEGATED))
            {
                var outName = MyPartMetadata.MOUNT_PREFIX + " " + srcName.Substring(MOUNT_DELEGATED.Length).Trim();
                var lTrans = new MatrixI(dest.BlockOrientation);
                MatrixI iTrans;
                MatrixI.Invert(ref lTrans, out iTrans);
                outName += " D:" + iTrans.GetDirection(Base6Directions.GetOppositeDirection(destDir)).ToString();
                var anchorPoint = source.Min + Base6Directions.GetIntVector(destDir);
                var del = anchorPoint - dest.Min;
                if (del != Vector3I.Zero)
                {
                    outName += " A:" + del.X + ":" + del.Y + ":" + del.Z;
                }
                if (string.IsNullOrWhiteSpace(dest.Name))
                    dest.Name = outName;
                else
                    dest.Name = dest.Name + MyPartMetadata.MULTI_USE_SENTINEL + outName;
                return true;
            }
            else if (srcName.StartsWithICase(RESERVED_SPACE_DELEGATED))
            {
                var baseName = srcName.Substring(RESERVED_SPACE_DELEGATED.Length).Trim();
                var args = baseName.Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                var box = MyPartDummyUtils.ParseReservedSpace(MyDefinitionManager.Static.GetCubeSize(grid.GridSizeEnum), source, args, (x, y) => SessionCore.LogBoth($"\"{srcName}\": " + x, y));
                var del = source.Min - (Vector3I)dest.Min;
                box.Box.Max += del;
                box.Box.Min += del;
                var boxLocalFloat = MyUtilities.TransformBoundingBox(box.Box, Matrix.Invert(new MatrixI(dest.BlockOrientation).GetFloatMatrix()));
                var boxLocal = new BoundingBoxI(Vector3I.Floor(boxLocalFloat.Min), Vector3I.Ceiling(boxLocalFloat.Max));
                var outName = $"{MyPartMetadata.RESERVED_SPACE_PREFIX} NE:{boxLocal.Min.X}:{boxLocal.Min.Y}:{boxLocal.Min.Z} PE:{boxLocal.Max.X}:{boxLocal.Max.Y}:{boxLocal.Max.Z}";
                if (box.IsShared)
                    outName += " shared";
                if (box.IsOptional)
                    outName += " optional";
                if (string.IsNullOrWhiteSpace(dest.Name))
                    dest.Name = outName;
                else
                    dest.Name = dest.Name + MyPartMetadata.MULTI_USE_SENTINEL + outName;
                return true;
            }
            return false;
        }

        public static void Process(IMyCubeGrid grid)
        {
            if (grid.CustomName == null || !grid.CustomName.StartsWithICase("EqProcBuild")) return;
            var ob = grid.GetObjectBuilder(true) as MyObjectBuilder_CubeGrid;
            if (ob == null) return;
            SessionCore.Log("Begin processing {0}", grid.CustomName);
            try
            {
                var dummyDel = new List<MyTuple<MyObjectBuilder_CubeBlock, string>>();
                var blockKeep = new List<MyObjectBuilder_CubeBlock>();
                var blockMap = new Dictionary<Vector3I, MyObjectBuilder_CubeBlock>(Vector3I.Comparer);
                foreach (var block in ob.CubeBlocks)
                {
                    var mount = false;
                    foreach (var name in block.ConfigNames())
                    {
                        if (!name.StartsWithICase(MOUNT_DELEGATED) && !name.StartsWithICase(RESERVED_SPACE_DELEGATED)) continue;
                        dummyDel.Add(MyTuple.Create(block, name));
                        mount = true;
                        break;
                    }
                    if (mount) continue;

                    var blockMin = (Vector3I)block.Min;
                    Vector3I blockMax;
                    BlockTransformations.ComputeBlockMax(block, out blockMax);
                    for (var rangeItr = new Vector3I_RangeIterator(ref blockMin, ref blockMax); rangeItr.IsValid(); rangeItr.MoveNext())
                        blockMap[rangeItr.Current] = block;
                    blockKeep.Add(block);
                }
                SessionCore.Log("Found {0} blocks to keep, {1} block mounts to remap", blockKeep.Count, dummyDel.Count);
                foreach (var pair in dummyDel)
                {
                    var block = pair.Item1;
                    var useName = pair.Item2;

                    IEnumerable<Base6Directions.Direction> dirs = Base6Directions.EnumDirections;
                    var def = MyDefinitionManager.Static.GetCubeBlockDefinition(pair.Item1);
                    var transform = new MatrixI(block.BlockOrientation);
                    if (def?.MountPoints != null)
                    {
                        var mountDirs = new HashSet<Base6Directions.Direction>();
                        foreach (var mount in def.MountPoints)
                            mountDirs.Add(Base6Directions.GetDirection(Vector3I.TransformNormal(mount.Normal, ref transform)));
                    }

                    var args = useName.Split(' ');
                    var keepArgs = new List<string>(args.Length);
                    foreach (var arg in args)
                        if (arg.StartsWithICase("D:"))
                        {
                            Base6Directions.Direction dir;
                            if (Enum.TryParse(arg.Substring(2), out dir))
                                dirs = new Base6Directions.Direction[] { transform.GetDirection(Base6Directions.GetOppositeDirection(dir)) };
                            else
                                SessionCore.LogBoth("Failed to parse direction argument \"{0}\"", arg);
                        }
                        else
                            keepArgs.Add(arg);
                    useName = string.Join(" ", keepArgs);

                    MyObjectBuilder_CubeBlock outputBlock = null;
                    var outputDir = Base6Directions.Direction.Forward;
                    foreach (var dir in dirs)
                    {
                        MyObjectBuilder_CubeBlock tmp;
                        if (!blockMap.TryGetValue(block.Min + Base6Directions.GetIntVector(dir), out tmp)) continue;
                        if (outputBlock != null)
                            SessionCore.LogBoth("Multiple directions found for {0}", pair.Item2);
                        outputBlock = tmp;
                        outputDir = dir;
                    }
                    if (outputBlock == null || !ApplyDelegate(ob, block, useName, outputBlock, outputDir))
                        SessionCore.LogBoth("Failed to find delegated mount point for {0}", pair.Item2);
                }
                ob.CubeBlocks = blockKeep;

                // Grab related grids!
                var relatedGrids = new HashSet<IMyCubeGrid> { grid };
                var scanRelated = new Queue<IMyCubeGrid>();
                var relatedGridController = new Dictionary<IMyCubeGrid, IMyCubeBlock>();
                scanRelated.Enqueue(grid);
                while (scanRelated.Count > 0)
                {
                    var subGrid = scanRelated.Dequeue();
                    IMyCubeBlock controllerForThisGrid = null;
                    relatedGridController.TryGetValue(subGrid, out controllerForThisGrid);

                    subGrid.GetBlocks(null, (y) =>
                    {
                        var x = y?.FatBlock;
                        if (x == null) return false;
                        var childGrid = (x as IMyMechanicalConnectionBlock)?.TopGrid;
                        if (childGrid != null && relatedGrids.Add(childGrid))
                        {
                            scanRelated.Enqueue(childGrid);
                            relatedGridController[childGrid] = x.CubeGrid == grid ? x : controllerForThisGrid;
                        }
                        var parentGrid = (x as IMyAttachableTopBlock)?.Base?.CubeGrid;
                        // ReSharper disable once InvertIf
                        if (parentGrid != null && relatedGrids.Add(parentGrid))
                        {
                            scanRelated.Enqueue(parentGrid);
                            relatedGridController[parentGrid] = x.CubeGrid == grid ? x : controllerForThisGrid;
                        }
                        return false;
                    });
                }
                relatedGrids.Remove(grid);
                var removedNoController = relatedGrids.RemoveWhere(x => !relatedGridController.ContainsKey(x));
                if (removedNoController > 0)
                    SessionCore.LogBoth("Failed to find controlling mechanical connection block for all subgrids.  {0} will be excluded", removedNoController);
                // Need to add reserved space for subgrids.  So compute that.  Yay!
                foreach (var rel in relatedGrids)
                {
                    IMyCubeBlock root;
                    if (!relatedGridController.TryGetValue(rel, out root))
                    {
                        SessionCore.LogBoth("Unable to find the controller for grid {0}", rel.CustomName);
                        continue;
                    }
                    MyObjectBuilder_CubeBlock blockDest;
                    if (blockMap.TryGetValue(root.Min, out blockDest))
                    {
                        var blockLocal = (MatrixD)new MatrixI(blockDest.BlockOrientation).GetFloatMatrix();
                        blockLocal.Translation = (Vector3I)blockDest.Min * grid.GridSize;
                        var blockWorld = MatrixD.Multiply(blockLocal, grid.WorldMatrix);

                        var worldAABB = rel.WorldAABB;
                        worldAABB = MyUtilities.TransformBoundingBox(worldAABB, MatrixD.Invert(blockWorld));
                        var gridAABB = new BoundingBoxI(Vector3I.Floor(worldAABB.Min / grid.GridSize), Vector3I.Ceiling(worldAABB.Max / grid.GridSize));
                        var code = $"{MyPartMetadata.RESERVED_SPACE_PREFIX} NE:{gridAABB.Min.X}:{gridAABB.Min.Y}:{gridAABB.Min.Z} PE:{gridAABB.Max.X}:{gridAABB.Max.Y}:{gridAABB.Max.Z}";
                        SessionCore.Log("Added reserved space for subgrid {0}: Spec is \"{1}\"", rel.CustomName, code);
                        if (blockDest.Name == null || blockDest.Name.Trim().Length == 0)
                            blockDest.Name = code;
                        else
                            blockDest.Name += MyPartMetadata.MULTI_USE_SENTINEL + code;
                    }
                    else
                    {
                        SessionCore.LogBoth("Unable to find the OB for grid block {0} ({1}, {2}, {3}).  Is it a delegate?", (root as IMyTerminalBlock)?.CustomName ?? root.Name, root.Min.X, root.Min.Y, root.Min.Z);
                    }
                }

                var allGrids = new List<MyObjectBuilder_CubeGrid>(relatedGrids.Count + 1) { ob };
                allGrids.AddRange(relatedGrids.Select(relGrid => relGrid.GetObjectBuilder(false)).OfType<MyObjectBuilder_CubeGrid>());

                // Compose description: TODO I'd love if this actually worked :/
                // var storage = new MyPartMetadata();
                // storage.InitFromGrids(ob, allGrids);
                // var data = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(storage.GetObjectBuilder()));

                var defOut = new MyObjectBuilder_PrefabDefinition()
                {
                    Id = new SerializableDefinitionId(typeof(MyObjectBuilder_PrefabDefinition), grid.CustomName),
                    CubeGrids = allGrids.ToArray()
                };

                var fileName = "export_" + grid.CustomName + ".sbc";
                SessionCore.LogBoth("Saving {1} grids as {0}", fileName, defOut.CubeGrids.Length);

                var mishMash = new MyObjectBuilder_Definitions()
                {
                    Prefabs = new MyObjectBuilder_PrefabDefinition[] { defOut }
                };
                var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(fileName, typeof(MyDesignTool));
                var obCode = MyAPIGateway.Utilities.SerializeToXML(mishMash);
                obCode = obCode.Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
                writer.Write(Encoding.UTF8.GetBytes(obCode));
                writer.Close();
            }
            catch (Exception e)
            {
                SessionCore.Log("Error {0}", e.ToString());
            }
        }
    }
}
