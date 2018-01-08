using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.Utils;
using Equinox.Utils.Command;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Exporter
{
    public class DesignTools : CommandProviderComponent
    {
        public const string DelegatedTag = "del";
        public const string MountDelegated = PartMetadata.MountPrefix + " " + DelegatedTag;
        public const string ReservedSpaceDelegated = PartMetadata.ReservedSpacePrefix + " " + DelegatedTag;

        private bool ApplyDelegate(MyObjectBuilder_CubeGrid grid, MyObjectBuilder_CubeBlock source, string srcName, MyObjectBuilder_CubeBlock dest, Base6Directions.Direction destDir)
        {
            if (srcName.StartsWithICase(MountDelegated))
            {
                var lTransOrig = new MatrixI(source.BlockOrientation);
                var lTrans = new MatrixI(dest.BlockOrientation);
                MatrixI iTrans;
                MatrixI.Invert(ref lTrans, out iTrans);
                var arguments = PartDummyUtils.ConfigArguments(srcName.Substring(MountDelegated.Length).Trim()).Select(
                    (arg) =>
                    {
                        if (arg.StartsWithICase(PartDummyUtils.ArgumentBiasDirection))
                        {
                            Base6Directions.Direction dir;
                            if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentBiasDirection.Length), out dir))
                                return PartDummyUtils.ArgumentBiasDirection +
                                       iTrans.GetDirection(lTransOrig.GetDirection(dir));
                            else
                                this.Error("Failed to parse bias argument \"{0}\"", arg);
                        }
                        else if (arg.StartsWithICase(PartDummyUtils.ArgumentSecondBiasDirection))
                        {
                            Base6Directions.Direction dir;
                            if (Enum.TryParse(arg.Substring(PartDummyUtils.ArgumentSecondBiasDirection.Length), out dir))
                                return PartDummyUtils.ArgumentSecondBiasDirection +
                                       iTrans.GetDirection(lTransOrig.GetDirection(dir));
                            else
                                this.Error("Failed to parse second bias argument \"{0}\"", arg);
                        }
                        return arg;
                    }).ToList();
                arguments.Add(PartDummyUtils.ArgumentMountDirection + iTrans.GetDirection(Base6Directions.GetOppositeDirection(destDir)));
                var anchorPoint = source.Min + Base6Directions.GetIntVector(destDir);
                var del = anchorPoint - dest.Min;
                if (del != Vector3I.Zero)
                    arguments.Add(PartDummyUtils.ArgumentAnchorPoint + del.X + ":" + del.Y + ":" + del.Z);
                var outName = PartMetadata.MountPrefix + " " + string.Join(" ", arguments);
                if (string.IsNullOrWhiteSpace(dest.Name))
                    dest.Name = outName;
                else
                    dest.Name = dest.Name + PartMetadata.MultiUseSentinel + outName;
                return true;
            }
            if (srcName.StartsWithICase(ReservedSpaceDelegated))
            {
                var baseName = srcName.Substring(ReservedSpaceDelegated.Length).Trim();
                var args = baseName.Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                var box = PartDummyUtils.ParseReservedSpace(MyDefinitionManager.Static.GetCubeSize(grid.GridSizeEnum), source, args, this.Error);
                var del = source.Min - (Vector3I)dest.Min;
                box.Box.Max += del;
                box.Box.Min += del;
                var boxLocalFloat = Utilities.TransformBoundingBox(box.Box, Matrix.Invert(new MatrixI(dest.BlockOrientation).GetFloatMatrix()));
                var boxLocal = new BoundingBoxI(Vector3I.Floor(boxLocalFloat.Min), Vector3I.Ceiling(boxLocalFloat.Max));
                var outName = $"{PartMetadata.ReservedSpacePrefix} NE:{boxLocal.Min.X}:{boxLocal.Min.Y}:{boxLocal.Min.Z} PE:{boxLocal.Max.X}:{boxLocal.Max.Y}:{boxLocal.Max.Z}";
                if (box.IsShared)
                    outName += " shared";
                if (box.IsOptional)
                    outName += " optional";
                if (string.IsNullOrWhiteSpace(dest.Name))
                    dest.Name = outName;
                else
                    dest.Name = dest.Name + PartMetadata.MultiUseSentinel + outName;
                return true;
            }
            return false;
        }

        private void Process(CommandFeedback feedback, IMyCubeGrid grid)
        {
            if (grid.CustomName == null || !grid.CustomName.StartsWithICase("EqProcBuild")) return;
            var ob = grid.GetObjectBuilder(true) as MyObjectBuilder_CubeGrid;
            if (ob == null) return;
            this.Info("Begin processing {0}", grid.CustomName);
            feedback?.Invoke("Processing {0}", grid.CustomName);
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
                        if (!name.StartsWithICase(MountDelegated) && !name.StartsWithICase(ReservedSpaceDelegated)) continue;
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
                this.Info("Found {0} blocks to keep, {1} block mounts to remap", blockKeep.Count, dummyDel.Count);
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
                        if (arg.StartsWithICase(PartDummyUtils.ArgumentMountDirection))
                        {
                            Base6Directions.Direction dir;
                            if (Enum.TryParse(arg.Substring(2), out dir))
                                dirs = new[] {transform.GetDirection(Base6Directions.GetOppositeDirection(dir))};
                            else
                            {
                                this.Error("Failed to parse direction argument \"{0}\"", arg);
                                feedback?.Invoke("Error: Failed to parse direction argument \"{0}\"", arg);
                            }
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
                        if (tmp.ConfigNames().Any(x => x.StartsWithICase(MountDelegated))) continue;
                        if (outputBlock != null)
                        {
                            this.Error("Multiple directions found for {0}", pair.Item2);
                            feedback?.Invoke("Error: Multiple directions found for {0}", pair.Item2);
                        }
                        outputBlock = tmp;
                        outputDir = dir;
                    }
                    if (outputBlock == null || !ApplyDelegate(ob, block, useName, outputBlock, outputDir))
                    {
                        this.Error("Failed to find delegated mount point for {0}", pair.Item2);
                        feedback?.Invoke("Error: Failed to find delegated mount point for {0}", pair.Item2);
                    }
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
                if (removedNoController > 0) {
                    this.Error("Failed to find the mechanical connection block for all subgrids.  {0} will be excluded",
                        removedNoController);
                    feedback?.Invoke("Error: Failed to find the mechanical connection block for all subgrids.  {0} will be excluded",
                        removedNoController);
                }
                // Need to add reserved space for subgrids so they don't overlap.  So compute that.  Yay!
                foreach (var rel in relatedGrids)
                {
                    IMyCubeBlock root;
                    if (!relatedGridController.TryGetValue(rel, out root))
                    {
                        this.Error("Unable to find the mechanical connection for grid {0}", rel.CustomName);
                        feedback?.Invoke("Error: Unable to find the mechanical connection for grid {0}",
                            rel.CustomName);
                        continue;
                    }
                    MyObjectBuilder_CubeBlock blockDest;
                    if (blockMap.TryGetValue(root.Min, out blockDest))
                    {
                        var blockLocal = (MatrixD)new MatrixI(blockDest.BlockOrientation).GetFloatMatrix();
                        blockLocal.Translation = (Vector3I)blockDest.Min * grid.GridSize;
                        var blockWorld = MatrixD.Multiply(blockLocal, grid.WorldMatrix);

                        var worldAABB = rel.WorldAABB;
                        worldAABB = Utilities.TransformBoundingBox(worldAABB, MatrixD.Invert(blockWorld));
                        var gridAABB = new BoundingBoxI(Vector3I.Floor(worldAABB.Min / grid.GridSize), Vector3I.Ceiling(worldAABB.Max / grid.GridSize));
                        var code = $"{PartMetadata.ReservedSpacePrefix} NE:{gridAABB.Min.X}:{gridAABB.Min.Y}:{gridAABB.Min.Z} PE:{gridAABB.Max.X}:{gridAABB.Max.Y}:{gridAABB.Max.Z}";
                        this.Info("Added reserved space for subgrid {0}: Spec is \"{1}\"", rel.CustomName, code);
                        if (blockDest.Name == null || blockDest.Name.Trim().Length == 0)
                            blockDest.Name = code;
                        else
                            blockDest.Name += PartMetadata.MultiUseSentinel + code;
                    }
                    else
                    {
                        this.Error("Unable to find the OB for grid block {0} ({1}, {2}, {3}).  Is it a delegate?", (root as IMyTerminalBlock)?.CustomName ?? root.Name, root.Min.X, root.Min.Y, root.Min.Z);
                        feedback?.Invoke("Unable to the find OB for grid block {0} ({1}, {2}, {3}).  Was it a delegate?", (root as IMyTerminalBlock)?.CustomName ?? root.Name, root.Min.X, root.Min.Y, root.Min.Z);
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

                var fileName = grid.CustomName + ".sbc";
                this.Info("Saving {1} grids as {0}", fileName, defOut.CubeGrids.Length);
                feedback?.Invoke("Saving {1} grids as {0}", fileName, defOut.CubeGrids.Length);

                var mishMash = new MyObjectBuilder_Definitions()
                {
                    Prefabs = new MyObjectBuilder_PrefabDefinition[] { defOut }
                };
                var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(fileName, typeof(DesignTools));
                var obCode = MyAPIGateway.Utilities.SerializeToXML(mishMash);
                obCode = obCode.Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
                writer.Write(Encoding.UTF8.GetBytes(obCode));
                writer.Close();
            }
            catch (Exception e)
            {
                this.Error("Failed to parse.  Error:\n{0}", e.ToString());
            }
        }

        public DesignTools()
        {
            Create("export").PromotedOnly(MyPromoteLevel.Admin).Handler(RunExport);
            // TODO a way to "sideload" prefabs from storage for testing
            Create("sideload").PromotedOnly(MyPromoteLevel.Admin).Handler<string>(RunSideload);
        }

        private string RunSideload(CommandFeedback feedback, string prefabKey)
        {
            var partManager = Manager.GetDependencyProvider<PartManager>();
            if (partManager == null)
                return "Can't sideload parts when there is no part manager";
            var fileName = prefabKey;
            if (!fileName.EndsWith(".sbc", StringComparison.OrdinalIgnoreCase))
                fileName += ".sbc";
            if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(fileName, typeof(DesignTools)))
                return $"File {fileName} has not been exported.";
            using (var stream = MyAPIGateway.Utilities.ReadFileInLocalStorage(fileName, typeof(DesignTools)))
            {
                var content = stream.ReadToEnd();
                try
                {
                    var data = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_Definitions>(content);
                    if (data.Prefabs == null || data.Prefabs.Length < 1)
                        return "The specified file doesn't seem to contain prefabs";
                    foreach (var prefab in data.Prefabs)
                    {
                        var lks = new MyPrefabDefinition();
                        // We don't actually link this into the definition manager so we can have a null mod context.
                        lks.Init(prefab, null);
                        lks.InitLazy(prefab);
                        partManager.Load(lks, true);
                        this.Debug("Sideloaded {0}", prefab.Id.SubtypeName);
                        feedback.Invoke("Sideloaded {0}", prefab.Id.SubtypeName);
                    }
                }
                catch (Exception e)
                {
                    this.Error("Failed to sideload prefab {0}.  Error:\n{1}", prefabKey, e.ToString());
                    return $"Failed to load: {e.Message}";
                }
            }
            return null;
        }

        private string RunExport(CommandFeedback feedback)
        {
            MyAPIGateway.Entities.GetEntities(null, x =>
            {
                var grid = x as IMyCubeGrid;
                if (grid != null)
                    Process(feedback, grid);
                return false;
            });
            return null;
        }


        public override void LoadConfiguration(Ob_ModSessionComponent config)
        {
            if (config == null) return;
            if (config is Ob_DesignTools) return;
            Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(), GetType());
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_DesignTools();
        }
    }

    public class Ob_DesignTools : Ob_ModSessionComponent
    {
    }
}
