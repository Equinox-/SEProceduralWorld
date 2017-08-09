using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Names;
using Equinox.ProceduralWorld.Voxels;
using Equinox.ProceduralWorld.Voxels.Asteroids;
using Equinox.Utils;
using Equinox.Utils.Command;
using Equinox.Utils.Logging;
using Equinox.Utils.Network;
using Equinox.Utils.Session;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class SessionCore : MyModSessionVRageAdapter
    {
        public static bool RELEASE = false;

        public static MyObjectBuilder_SessionManager DefaultConfiguration()
        {
            var res = new MyObjectBuilder_SessionManager();
            res.SessionComponents = new List<MyObjectBuilder_ModSessionComponent>();
            res.SessionComponents.Add(new MyObjectBuilder_CustomLogger() { Filename = "ProceduralWorld.log", LogLevel = MyLogSeverity.Debug });
            res.SessionComponents.Add(new MyObjectBuilder_CommandDispatch());
            res.SessionComponents.Add(new MyObjectBuilder_Network());
            res.SessionComponents.Add(new MyObjectBuilder_RPC());
            res.SessionComponents.Add(new MyObjectBuilder_ProceduralWorldManager());
            res.SessionComponents.Add(new MyObjectBuilder_PartManager());
            res.SessionComponents.Add(new MyObjectBuilder_BuildingControlCommands());
            res.SessionComponents.Add(new MyObjectBuilder_ProceduralFactions());
            res.SessionComponents.Add(new MyObjectBuilder_StationGeneratorManager());
            res.SessionComponents.Add(new MyObjectBuilder_CompositeNameGenerator()
            {
                Generators = new List<MyObjectBuilder_CompositeNameGeneratorEntry>()
                {
                    new MyObjectBuilder_CompositeNameGeneratorEntry(){Generator = new MyObjectBuilder_StatisticalNameGenerator(), Weight = 0.9f},
                    new MyObjectBuilder_CompositeNameGeneratorEntry(){Generator = new MyObjectBuilder_ExoticNameGenerator(), Weight = 0.1f}
                }
            });

            //                        res.SessionComponents.Add(new MyObjectBuilder_ProceduralStation());
            return res;
        }

        public static SessionCore Instance { get; private set; }

        public MyPartManager PartManager => Manager.GetDependencyProvider<MyPartManager>();
        public Settings Settings { get; }
        public SessionCore()
        {
            Settings = new Settings();
            Instance = this;
        }

        private bool m_init = false;
        public override void UpdateBeforeSimulation()
        {
            if (!m_init)
            {
                try
                {
                    Manager.Register(new MySessionBootstrapper());
                    if (MyAPIGateway.Session.IsDecider())
                    {
                        var success = false;
                        if (RELEASE)
                            try
                            {
                                if (MyAPIGateway.Utilities.FileExistsInWorldStorage("session.xml", typeof(SessionCore)))
                                {
                                    using (var reader =
                                        MyAPIGateway.Utilities.ReadFileInWorldStorage("session.xml",
                                            typeof(SessionCore)))
                                    {
                                        var value = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_SessionManager>(reader.ReadToEnd());
                                        Manager.AppendConfiguration(value);
                                        success = true;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log("Failed to parse config:\n{0}", e.ToString());
                            }
                        if (!success)
                            Manager.AppendConfiguration(DefaultConfiguration());
                    }
                }
                catch (Exception e)
                {
                    SessionCore.Log("Failed to start bootstrapper.\n{0}", e);
                }
            }
            base.UpdateBeforeSimulation();
            if (!m_init)
            {
                try
                {
                    var config = MyAPIGateway.Utilities.SerializeToXML(Manager.SaveConfiguration());
                    var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("session.xml", typeof(SessionCore));
                    writer.Write(config);
                    writer.Close();
                }
                catch (Exception e)
                {
                    SessionCore.Log("Failed to write default configuration.\n{0}", e);
                }
                m_init = true;
            }
        }

        public static MyLoggerBase Logger => Instance?.Manager.GetDependencyProvider<MyLoggerBase>();

        public static void Log(string format, params object[] args)
        {
            var logger = Instance?.Manager.GetDependencyProvider<MyLoggerBase>();
            if (logger != null)
                logger.Info(format, args);
            else
                MyLog.Default?.Log(MyLogSeverity.Info, format, args);
        }

        public static void LogBoth(string fmt, params object[] args)
        {
            Log(fmt, args);
            MyAPIGateway.Utilities.ShowMessage("Exporter", string.Format(fmt, args));
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
    }
}
