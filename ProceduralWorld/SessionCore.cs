using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ProceduralWorld.Buildings;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Exporter;
using Equinox.ProceduralWorld.Buildings.Game;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Utils.Logging;
using Equinox.ProceduralWorld.Utils.Session;
using Equinox.ProceduralWorld.Voxels;
using Equinox.Utils;
using Equinox.Utils.Command;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    internal class SessionCore : MyModSessionVRageAdapter
    {
        public static SessionCore Instance { get; private set; }
        
        public MyCustomLogger FileLogger { get; }
        public MyPartManager PartManager { get; }
        public MyProceduralWorldManager WorldManager { get; }
        public MyCommandDispatchComponent CommandDispatch { get; }
        public Settings Settings { get; }
        public SessionCore()
        {
            FileLogger = new MyCustomLogger("ProceduralWorld.log");
            PartManager = new MyPartManager();
            WorldManager = new MyProceduralWorldManager();
            Settings = new Settings();
            CommandDispatch = new MyCommandDispatchComponent();

            Manager.Register(FileLogger);
            Manager.Register(PartManager);
            Manager.Register(WorldManager);
            Manager.Register(CommandDispatch);
            Manager.Register(new MyBuildingDebugCommands());
        }

        public static void Log(string format, params object[] args)
        {
            if (Instance?.FileLogger != null)
                Instance.FileLogger.Info(format, args);
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
