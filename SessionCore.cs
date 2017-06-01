using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Exporter;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class SessionCore : MySessionComponentBase
    {
        public static SessionCore Instance { get; private set; }

        public static void Log(string format, params object[] args)
        {
            if (Instance?.Logger != null)
                Instance.Logger.Log(format, args);
            else
                MyLog.Default?.Log(MyLogSeverity.Info, format, args);
        }

        public static void LogBoth(string fmt, params object[] args)
        {
            SessionCore.Log(fmt, args);
            MyAPIGateway.Utilities.ShowMessage("Exporter", string.Format(fmt, args));
        }

        public override void LoadData()
        {
            base.LoadData();
        }

        private bool m_attached = false;
        public MyPartManager PartManager { get; private set; }
        public Logging Logger { get; private set; }
        public Settings Settings { get; private set; }
        public MyProceduralWorldManager WorldManager { get; private set; }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (MyAPIGateway.Session == null) return;
            if (MyAPIGateway.Session.Player == null) return;
            if (!m_attached)
                Attach();
            WorldManager.UpdateBeforeSimulation();
            Logger.OnUpdate();
        }

        public override void Draw()
        {
            MyAPIGateway.Entities?.GetEntities(null, (x) =>
            {
                var component = x?.Components?.Get<MyProceduralGridComponent>();
                component?.DebugDraw();
                return false;
            });
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (m_attached)
                Detach();
        }

        private void Attach()
        {
            m_attached = true;
            Instance = this;
            try
            {
                Settings = new Settings();
                Logger = new Logging("ProceduralBuilding.log");
                PartManager = new MyPartManager();
                PartManager.LoadAll();

                MyAPIGateway.Utilities.ShowNotification(PartManager.Count() + " procedural modules ready");
                MyAPIGateway.Utilities.MessageEntered += CommandDispatcher;
                WorldManager = new MyProceduralWorldManager();
            }
            catch (Exception e)
            {
                Log("Fatal error loading Procedural Buildings: \n{0}", e);
            }
        }

        private void Detach()
        {
            m_attached = false;

            WorldManager.Unload();
            WorldManager = null;
            MyAPIGateway.Utilities.MessageEntered -= CommandDispatcher;

            Logger?.Close();
            Logger = null;
            PartManager = null;
            Instance = null;
        }

        private static string FormatMatrix(MatrixI m2)
        {
            var m = m2.GetFloatMatrix();
            return $"{m.M11,5} {m.M12,5} {m.M13,5} {m.Translation.X,5}\r\n{m.M21,5} {m.M22,5} {m.M23,5} {m.Translation.Y,5}\r\n{m.M31,5} {m.M32,5} {m.M33,5} {m.Translation.Z,5}";
        }

        private void CommandDispatcher(string messageText, ref bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer || !messageText.StartsWith("/")) return;
            var args = messageText.Split(' ');
            if (args[0].Equals("/export"))
            {
                MyAPIGateway.Entities.GetEntities(null, x =>
                {
                    var grid = x as IMyCubeGrid;
                    if (grid != null)
                        MyDesignTool.Process(grid);
                    return false;
                });
                return;
            }
            try
            {
                if (args[0].Equals("/part"))
                    ProcessDebugPart(args);
                if (args[0].Equals("/list"))
                    ProcessSpawn(args);
                if (args[0].Equals("/info"))
                    ProcessInfo(args);
                if (args[0].Equals("/stations"))
                    ProcessStationLocations(args);
                if (args[0].Equals("/clear"))
                    ClearStations();
            }
            catch (Exception e)
            {
                Log("Error processing {0}:\n{1}", messageText, e.ToString());
            }
        }

        private void ClearStations()
        {
            var id = MyAPIGateway.Session.Player.IdentityId;
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(id))
                MyAPIGateway.Session.GPS.RemoveGps(id, gps);
            var ent = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(ent, (x) => x is IMyCubeGrid);
            foreach (var k in ent)
                MyAPIGateway.Entities.RemoveEntity(k);
        }

        private void ProcessStationLocations(IReadOnlyList<string> args)
        {
            var sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, MyAPIGateway.Session.SessionSettings.ViewDistance * 10);
            var id = MyAPIGateway.Session.Player.IdentityId;
            foreach (var gps in MyAPIGateway.Session.GPS.GetGpsList(id))
                if (gps.Name.StartsWith("Station - "))
                    MyAPIGateway.Session.GPS.RemoveGps(id, gps);
            var aabb = new BoundingBoxD(sphere.Center - sphere.Radius, sphere.Center + sphere.Radius);
            foreach (var s in MyProceduralWorld.Instance.StationNoise.TryGetSpawnIn(aabb, sphere.Intersects))
            {
                var position = new Vector3D(s.Item2.X, s.Item2.Y, s.Item2.Z);
                var cseed = new MyProceduralConstructionSeed(s.Item2, null, s.Item1.GetHashCode());
                var gps = MyAPIGateway.Session.GPS.Create("[" + cseed.Faction.Tag + "] " + cseed.Name, "", position, true, true);
                gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + new TimeSpan(0, 5, 0);
                MyAPIGateway.Session.GPS.AddGps(id, gps);
            }
        }

        private void ProcessInfo(IReadOnlyList<string> args)
        {
            if (args.Count < 2)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Usage: " + args[0] + " [part name]");
                return;
            }
            var part = PartManager.FirstOrDefault(test => test.Prefab.Id.SubtypeName.ToLower().Contains(args[1].ToLower()));
            if (part == null)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Unable to find part with name \"" + args[1] + "\"");
                return;
            }
            var info = part.BlockSetInfo;
            SessionCore.Log("Part info for {0}\nBlock type counts:", part.Name);
            foreach (var kv in info.BlockCountByType)
                SessionCore.Log("{0}: {1}", kv.Key, kv.Value);
            SessionCore.Log("Total power consumption/storage: {0:e}:{1:e}  Groups:", info.TotalPowerNetConsumption, info.TotalPowerStorage);
            foreach (var kv in info.PowerConsumptionByGroup)
                SessionCore.Log("{0}: {1}", kv.Key, kv.Value);
            SessionCore.Log("Total inventory capacity: {0:e}", info.TotalInventoryCapacity);
            SessionCore.Log("Total component cost:");
            foreach (var kv in info.ComponentCost)
                SessionCore.Log("{0}: {1}", kv.Key.Id.SubtypeName, kv.Value);
            SessionCore.Log("Production quotas:");
            foreach (var pi in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
                SessionCore.Log("{0}: {1}", pi.Id, info.TotalProduction(pi.Id));
            foreach (var pi in MyDefinitionManager.Static.GetDefinitionsOfType<MyComponentDefinition>())
                SessionCore.Log("{0}: {1}", pi.Id, info.TotalProduction(pi.Id));
            foreach (var gas in MyDefinitionManager.Static.GetDefinitionsOfType<MyGasProperties>())
                SessionCore.Log("{0}: {1}", gas.Id, info.TotalProduction(gas.Id));
            SessionCore.Log("Gas storage");
            foreach (var gas in MyDefinitionManager.Static.GetDefinitionsOfType<MyGasProperties>())
                SessionCore.Log("{0}: {1}", gas.Id, info.TotalGasStorage(gas.Id));
        }

        private void ProcessDebugPart(IReadOnlyList<string> args)
        {
            if (args.Count < 2)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Usage: " + args[0] + " [part name]");
                return;
            }
            var part = PartManager.FirstOrDefault(test => test.Prefab.Id.SubtypeName.ToLower().Contains(args[1].ToLower()));
            if (part == null)
            {
                MyAPIGateway.Utilities.ShowMessage("PDebug", "Unable to find part with name \"" + args[1] + "\"");
                return;
            }
            var position = MyAPIGateway.Session.Camera.Position + MyAPIGateway.Session.Camera.WorldMatrix.Forward * 100;
            var seed = new MyProceduralConstructionSeed(new Vector4D(position, 0.5), null, 0);

            MyAPIGateway.Parallel.Start(() =>
            {
                var construction = new MyProceduralConstruction(seed);
                construction.GenerateRoom(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
                var remapper = new MyRoomRemapper { DebugRoomColors = true };
                var grids = MyGridCreator.RemapAndBuild(construction, remapper);
                if (grids == null) return;
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    var component = grids.Spawn();
                    if (component != null)
                        component.ForceDebugDraw = true;
                });
            });
        }

        private void ProcessSpawn(IReadOnlyList<string> args)
        {
            var count = 2;
            if (args.Count >= 2)
                int.TryParse(args[1].Trim(), out count);
            var position = MyAPIGateway.Session.Camera.Position + MyAPIGateway.Session.Camera.WorldMatrix.Forward * 100;
            var seed = new MyProceduralConstructionSeed(new Vector4D(position, 0.5), null, 0);
            MyAPIGateway.Parallel.Start(() =>
            {
                MyProceduralConstruction construction = null;
                MyConstructionCopy grids;
                if (!MyGenerator.GenerateFully(seed, ref construction, out grids)) return;
                if (grids == null) return;
                MyAPIGateway.Utilities.InvokeOnGameThread(() => grids.Spawn());
            });
        }
    }
}
