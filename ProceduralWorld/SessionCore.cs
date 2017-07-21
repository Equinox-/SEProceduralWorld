using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Buildings;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Manager;
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
using MyObjectBuilder_AsteroidField = Equinox.ProceduralWorld.Voxels.Asteroids.MyObjectBuilder_AsteroidField;

namespace Equinox.ProceduralWorld
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class SessionCore : MyModSessionVRageAdapter
    {
        public static MyObjectBuilder_SessionManager DefaultConfiguration()
        {
            var res = new MyObjectBuilder_SessionManager();
            res.SessionComponents = new List<MyObjectBuilder_ModSessionComponent>();
            res.SessionComponents.Add(new MyObjectBuilder_CustomLogger() { Filename = "ProceduralWorld.log", LogLevel = MyLogSeverity.Debug });
            res.SessionComponents.Add(new MyObjectBuilder_CommandDispatch());
            res.SessionComponents.Add(new MyObjectBuilder_Network());
            res.SessionComponents.Add(new MyObjectBuilder_RPC());
            res.SessionComponents.Add(new MyObjectBuilder_PartManager());
            res.SessionComponents.Add(new MyObjectBuilder_BuildingControlCommands());
            res.SessionComponents.Add(new MyObjectBuilder_ProceduralWorldManager());
            //            res.SessionComponents.Add(new MyObjectBuilder_ProceduralStation());
            res.SessionComponents.Add(new MyObjectBuilder_AutomaticAsteroidFields()
            {
                AsteroidFields =
                {
                    new MyObjectBuilder_AutoAsteroidField()
                    {
                        Field = new MyObjectBuilder_AsteroidField()
                        {
                            DensityRegionSize=1,
                            Seed = 1,
                            Layers = new[]
                            {
                                new MyAsteroidLayer() {AsteroidDensity = 0.5, AsteroidMaxSize = 1e3, AsteroidMinSize = 500, AsteroidSpacing = 3.2e3, UsableRegion = 0.5},
                                new MyAsteroidLayer() {AsteroidDensity = 0.8, AsteroidMaxSize = 500, AsteroidMinSize = 250, AsteroidSpacing = 1.4e3, UsableRegion = 1}
                            },
                            ShapeRing = new MyObjectBuilder_AsteroidRing() { InnerRadius=1.9f, OuterRadius=2.1f, VerticalScaleMult=0.1f },
                            Transform = new MyPositionAndOrientation(MatrixD.Identity)
                        },
                        OnPlanets = {new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "EarthLike")}
                    }
                }
            });
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
                        Manager.AppendConfiguration(DefaultConfiguration());
                }
                catch (Exception e)
                {
                    SessionCore.Log("Failed to start bootstrapper.\n{0}", e);
#if DEBUG
                    throw;
#endif
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
#if DEBUG
                    throw;
#endif
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

        //            MyAPIGateway.Entities.GetEntities(null, (x) =>
        //            {
        //                if (!(x is IMyOxygenProvider) || !(x is IMyVoxelBase)) return false;
        //                var center = x.PositionComp.WorldMatrix;
        //                center.Translation = x.PositionComp.WorldAABB.Center;
        //                var radiusBase = x.WorldAABB.HalfExtents.Length() * 2;
        //                var vertScale = 0.1;
        //                var baseSpacing = Math.Min(5e3, radiusBase / 10);
        //                var width = baseSpacing / vertScale;
        ////                m_modules.Add(new MyAsteroidFieldModule()
        ////                {
        ////                    Layers = new[]
        ////                    {
        ////                        new MyAsteroidLayer() {AsteroidDensity = 0.5, AsteroidMaxSize = 1e3, AsteroidMinSize = 500, AsteroidSpacing = baseSpacing/2, UsableRegion = 0.5},
        ////                        new MyAsteroidLayer() {AsteroidDensity = 0.9, AsteroidMaxSize = 500, AsteroidMinSize = 50, AsteroidSpacing = baseSpacing/4, UsableRegion = 1},
        ////                    },
        ////                    RingInnerRadius = radiusBase - width,
        ////                    RingOuterRadius = radiusBase + width,
        ////                    VerticalScaleMult = vertScale,
        ////                    Transform = center
        ////                });
        //                Log("Ring at {0}", center.Translation + center.Forward * radiusBase);
        //                return false;
        //            });
    }
}
