using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class ConstructionCopy
    {
        public readonly ProceduralConstruction Construction;
        public readonly MyObjectBuilder_CubeGrid PrimaryGrid;
        public readonly List<MyObjectBuilder_CubeGrid> AuxGrids;
        public readonly ILogging Logger;
        private readonly RoomRemapper m_remapper;
        public BoundingBoxD BoundingBox;

        public ConstructionCopy(ProceduralRoom room, RoomRemapper remapper = null)
        {
            Logger = room.Owner.Logger.Root().CreateProxy(GetType().Name);
            var i = room.Part.PrimaryGrid;
            var o = new MyObjectBuilder_CubeGrid
            {
                GridSizeEnum = i.GridSizeEnum,
                IsStatic = true,
                DampenersEnabled = true,
                Handbrake = true,
                DisplayName = room.Owner.Seed.Name,
                DestructibleBlocks = true,
                IsRespawnGrid = false,
                Editable = true,
                PersistentFlags = MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene | MyPersistentEntityFlags2.CastShadows,
                PositionAndOrientation = new MyPositionAndOrientation(GridCreator.WorldTransformFor(room.Owner))
            };
            BoundingBox = BoundingBoxD.CreateInvalid();

            Construction = room.Owner;
            PrimaryGrid = o;
            AuxGrids = new List<MyObjectBuilder_CubeGrid>();
            m_remapper = remapper ?? new RoomRemapper(Logger.Root());

            var iwatch = new Stopwatch();
            m_remapper.Remap(room, this);
            Logger.Debug("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
        }

        private static readonly MyConcurrentPool<List<MyEntity>> m_entityListPool = new MyConcurrentPool<List<MyEntity>>(8);
        private static bool Conflicts(MyObjectBuilder_CubeGrid grid)
        {
            // Credits for this to KSH GitHub.
            var gridSize = MyDefinitionManager.Static.GetCubeSize(grid.GridSizeEnum);
            var localBb = new BoundingBox(Vector3.MaxValue, Vector3.MinValue);
            foreach (var block in grid.CubeBlocks)
            {
                MyCubeBlockDefinition definition;
                if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out definition)) continue;
                MyBlockOrientation ori = block.BlockOrientation;
                var blockSize = Vector3.TransformNormal(new Vector3(definition.Size) * gridSize, ori);
                blockSize = Vector3.Abs(blockSize);

                var minCorner = new Vector3(block.Min) * gridSize - new Vector3(gridSize / 2);
                var maxCorner = minCorner + blockSize;

                localBb.Include(minCorner);
                localBb.Include(maxCorner);
            }

            var worldAABB = ((BoundingBoxD)localBb).TransformFast(grid.PositionAndOrientation?.GetMatrix() ?? MatrixD.Identity);
            var list = m_entityListPool.Get();
            list.Clear();
            MyGamePruningStructure.GetTopMostEntitiesInBox(ref worldAABB, list);
            foreach (var k in list)
            {
                var g = k as IMyCubeGrid;
                var voxel = (k as IMyVoxelBase)?.PositionComp;
                if (g != null && g.WorldAABB.Intersects(worldAABB)) return true;
                if (voxel != null && voxel.WorldVolume.Intersects(worldAABB)) return true;
            }
            list.Clear();
            return false;
        }

        public bool Conflicts()
        {
            if (Conflicts(PrimaryGrid)) return true;
            // ReSharper disable once InconsistentlySynchronizedField
            foreach (var g in AuxGrids)
                if (Conflicts(g))
                    return true;
            return false;
        }

        private static void FlagIfReady(long myID, HashSet<long> dirtyGrids, IReadOnlyList<IMyCubeGrid> grids)
        {
            lock (dirtyGrids)
            {
                var removed = dirtyGrids.Remove(myID);
                if (removed && dirtyGrids.Count == 0)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        foreach (var grid in grids)
                            grid.Components.Get<ProceduralGridComponent>()?.UpdateReadyState();
                    });
            }
        }

        public void AppendRoom(ProceduralRoom room)
        {
            var iwatch = new Stopwatch();
            m_remapper.Remap(room, this);
            Logger.Debug("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
        }

        private static IMyEntity CreateFromObjectBuilderShim(MyObjectBuilder_EntityBase ob, bool addToScene,
            Action callback)
        {
            ob.PersistentFlags |= MyPersistentEntityFlags2.InScene;
            return MyAPIGateway.Entities.CreateFromObjectBuilderParallel(ob, addToScene, callback);
        }

        public bool IsRegionEmpty()
        {
            return !MyAPIGateway.Entities.GetTopMostEntitiesInBox(ref BoundingBox).Any(x => x is IMyCubeGrid || x is IMyVoxelBase);
        }

        public ProceduralGridComponent SpawnAsync()
        {
            var dirtyGrids = new HashSet<long>();

            var allGrids = new List<IMyCubeGrid>();
            // This ensures that dirtyGrids gets populated before the spawning callback runs.
            lock (dirtyGrids)
            {
                var iwatch = new Stopwatch();
                iwatch.Restart();
                PrimaryGrid.IsStatic = true;
                var primaryGrid = CreateFromObjectBuilderShim(PrimaryGrid, true, () => FlagIfReady(PrimaryGrid.EntityId, dirtyGrids, allGrids)) as IMyCubeGrid;
                if (primaryGrid == null)
                {
                    Logger.Error("Failed to remap primary entity.  Aborting spawn.");
                    return null;
                }
                dirtyGrids.Add(PrimaryGrid.EntityId);
                allGrids.Add(primaryGrid);
                Logger.Debug("Created entity for {0} room grid in {1}", Construction.Rooms.Count(), iwatch.Elapsed);
                if (Settings.AllowAuxillaryGrids)
                {
                    iwatch.Restart();
                    foreach (var aux in AuxGrids)
                    {
                        // ReSharper disable once ImplicitlyCapturedClosure
                        var res = CreateFromObjectBuilderShim(aux, true,
                            () => FlagIfReady(aux.EntityId, dirtyGrids, allGrids)) as IMyCubeGrid;
                        if (res == null)
                        {
                            Logger.Warning("Failed to remap secondary entity.  Skipping.");
                            continue;
                        }
                        allGrids.Add(res);
                        dirtyGrids.Add(aux.EntityId);
                    }
                    if (AuxGrids.Count > 0)
                        Logger.Debug("Created {0} aux grids in {1}", AuxGrids.Count, iwatch.Elapsed);
                }
                var component = new ProceduralGridComponent(Construction, allGrids);
                primaryGrid.Components.Add(component);
                return component;
            }
        }
    }

    public static class GridCreator
    {
        public static MatrixD WorldTransformFor(ProceduralConstruction construction)
        {
            var room = construction.Rooms.First();
            var spawnLocation = construction.Seed.WorldMatrix;
            spawnLocation.Translation -= Vector3D.TransformNormal(room.BoundingBox.Center, ref spawnLocation) * MyDefinitionManager.Static.GetCubeSize(room.Part.PrimaryCubeSize);
            spawnLocation.Translation -= room.Part.Prefab.BoundingSphere.Center;
            return spawnLocation;
        }

        public static ConstructionCopy RemapAndBuild(ProceduralConstruction construction, RoomRemapper remapper = null)
        {
            ConstructionCopy grids = null;
            foreach (var room in construction.Rooms)
            {
                if (grids == null)
                    grids = new ConstructionCopy(room, remapper);
                else
                    grids.AppendRoom(room);
            }
            return grids;
        }
    }
}
