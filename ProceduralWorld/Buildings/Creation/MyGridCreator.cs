using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Storage;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class MyConstructionCopy
    {
        public readonly MyProceduralConstruction Construction;
        public readonly MyObjectBuilder_CubeGrid PrimaryGrid;
        public readonly List<MyObjectBuilder_CubeGrid> AuxGrids;

        public MyConstructionCopy(MyProceduralConstruction c, MyObjectBuilder_CubeGrid primaryGrid)
        {
            Construction = c;
            PrimaryGrid = primaryGrid;
            AuxGrids = new List<MyObjectBuilder_CubeGrid>();
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
                if (g == null) continue;
                if (g.WorldAABB.Intersects(worldAABB)) return true;
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
                        {
                            grid.PersistentFlags |= MyPersistentEntityFlags2.InScene;
                            grid.OnAddedToScene(grid);
                        }
                        foreach (var grid in grids)
                            grid.Components.Get<MyProceduralGridComponent>()?.UpdateReadyState();
                    });
            }
        }

        public MyProceduralGridComponent SpawnAsync()
        {
            SessionCore.Log("Starting spawn state ");
            var dirtyGrids = new HashSet<long>();

            var allGrids = new List<IMyCubeGrid>();
            // This ensures that dirtyGrids gets populated before the spawning callback runs.
            lock (dirtyGrids)
            {
                var iwatch = new Stopwatch();
                iwatch.Restart();
                PrimaryGrid.IsStatic = true;
                PrimaryGrid.PersistentFlags &= ~MyPersistentEntityFlags2.InScene;
                var primaryGrid = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(PrimaryGrid, true, () => FlagIfReady(PrimaryGrid.EntityId, dirtyGrids, allGrids)) as IMyCubeGrid;
                if (primaryGrid == null)
                {
                    SessionCore.Log("Failed to remap primary entity.  Aborting.");
                    return null;
                }
                dirtyGrids.Add(PrimaryGrid.EntityId);
                allGrids.Add(primaryGrid);
                SessionCore.Log("Created entity for {0} room grid in {1}", Construction.Rooms.Count(), iwatch.Elapsed);
                iwatch.Restart();
                foreach (var aux in AuxGrids)
                {
                    aux.PersistentFlags &= ~MyPersistentEntityFlags2.InScene;
                    // ReSharper disable once ImplicitlyCapturedClosure
                    var res = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(aux, true, () => FlagIfReady(aux.EntityId, dirtyGrids, allGrids)) as IMyCubeGrid;
                    if (res == null)
                    {
                        SessionCore.Log("Failed to remap secondary entity.  Skipping.");
                        continue;
                    }
                    allGrids.Add(res);
                    dirtyGrids.Add(aux.EntityId);
                }
                if (AuxGrids.Count > 0)
                    SessionCore.Log("Created {0} aux grids in {1}", AuxGrids.Count, iwatch.Elapsed);
                var component = new MyProceduralGridComponent(Construction, allGrids);
                primaryGrid.Components.Add(component);
                return component;
            }
        }
    }

    public static class MyGridCreator
    {
        public static MatrixD WorldTransformFor(MyProceduralConstruction construction)
        {
            var room = construction.Rooms.First();
            var spawnLocation = construction.Seed.WorldMatrix;
            spawnLocation.Translation -= Vector3D.TransformNormal(room.BoundingBox.Center, ref spawnLocation) * MyDefinitionManager.Static.GetCubeSize(room.Part.PrimaryCubeSize);
            spawnLocation.Translation -= room.Part.Prefab.BoundingSphere.Center;
            return spawnLocation;
        }

        public static MyConstructionCopy SpawnRoomAt(MyProceduralRoom room, MyRoomRemapper remapper)
        {
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
                PositionAndOrientation = new MyPositionAndOrientation(WorldTransformFor(room.Owner))
            };

            var output = new MyConstructionCopy(room.Owner, o);
            remapper.Remap(room, output);
            return output;
        }

        public static void AppendRoom(MyConstructionCopy dest, MyProceduralRoom room, MyRoomRemapper remapper)
        {
            remapper.Remap(room, dest);
        }

        public static MyConstructionCopy RemapAndBuild(MyProceduralConstruction construction, MyRoomRemapper remapper = null)
        {
            if (remapper == null) remapper = new MyRoomRemapper();
            MyConstructionCopy grids = null;
            var iwatch = new Stopwatch();
            foreach (var room in construction.Rooms)
            {
                iwatch.Restart();
                if (grids == null)
                    grids = SpawnRoomAt(room, remapper);
                else
                    AppendRoom(grids, room, remapper);
                if (Settings.Instance.DebugGenerationResults)
                    SessionCore.Log("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
            }
            return grids;
        }
    }
}
