using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Storage;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
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

        public MyProceduralGridComponent Spawn()
        {
            var allGrids = new List<IMyCubeGrid>();
            var iwatch = new Stopwatch();
            iwatch.Restart();
            var primaryGrid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(PrimaryGrid) as IMyCubeGrid;
            if (primaryGrid == null) return null;
            allGrids.Add(primaryGrid);
            primaryGrid.IsStatic = true;
            SessionCore.Log("Spawned entity for {0} room grid in {1}", Construction.Rooms.Count(), iwatch.Elapsed);
            iwatch.Restart();
            allGrids.AddRange(AuxGrids.Select(aux => MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(aux)).OfType<IMyCubeGrid>());
            if (AuxGrids.Count > 0)
                SessionCore.Log("Spawned {0} aux grids in {1}", AuxGrids.Count, iwatch.Elapsed);
            var component = new MyProceduralGridComponent(Construction, allGrids);
            primaryGrid.Components.Add(component);
            return component;
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
            if (remapper ==null) remapper = new MyRoomRemapper();
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
