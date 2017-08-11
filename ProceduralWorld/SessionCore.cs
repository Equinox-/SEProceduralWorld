using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings;
using Equinox.ProceduralWorld.Buildings.Exporter;
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
            res.SessionComponents.Add(new MyObjectBuilder_DesignTools());

            //                        res.SessionComponents.Add(new MyObjectBuilder_ProceduralStation());
            return res;
        }

        public MyPartManager PartManager => Manager.GetDependencyProvider<MyPartManager>();
        public Settings Settings { get; }
        public SessionCore()
        {
            Settings = new Settings();
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
                                Logger.Error("Failed to parse config:\n{0}", e.ToString());
                            }
                        if (!success)
                            Manager.AppendConfiguration(DefaultConfiguration());
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to start bootstrapper.\n{0}", e);
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
                    Logger.Error("Failed to write default configuration.\n{0}", e);
                }
                m_init = true;
            }
        }

        private IMyLoggingBase Logger => Manager.FallbackLogger;
        
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
