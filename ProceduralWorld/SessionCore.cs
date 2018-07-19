using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
using Equinox.ProceduralWorld.Voxels.Planets;
using Equinox.ProceduralWorld.Voxels.VoxelBuilder;
using Equinox.Utils;
using Equinox.Utils.Command;
using Equinox.Utils.Logging;
using Equinox.Utils.Network;
using Equinox.Utils.Session;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Library.Collections;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
    public class SessionCore : ModSessionVRageAdapter
    {
        public const bool RELEASE = true;

        public static MyObjectBuilder_SessionManager DefaultConfiguration()
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var res = new MyObjectBuilder_SessionManager();
            // ReSharper disable once UseObjectOrCollectionInitializer
            res.SessionComponents = new List<Ob_ModSessionComponent>();
            res.SessionComponents.Add(new Ob_CustomLogger() {Filename = "ProceduralWorld.log", LogLevel = MyLogSeverity.Debug});
            if (!RELEASE)
            {
                res.SessionComponents.Add(new Ob_CommandDispatch());
                res.SessionComponents.Add(new Ob_Network());
                res.SessionComponents.Add(new Ob_RPC());
                res.SessionComponents.Add(new Ob_ProceduralWorldManager());
                res.SessionComponents.Add(new Ob_PartManager());
                res.SessionComponents.Add(new Ob_BuildingControlCommands());
                res.SessionComponents.Add(new Ob_ProceduralFactions());
                res.SessionComponents.Add(new Ob_StationGeneratorManager());
                res.SessionComponents.Add(new Ob_CompositeNameGenerator()
                {
                    Generators = new List<Ob_CompositeNameGeneratorEntry>()
                    {
                        new Ob_CompositeNameGeneratorEntry()
                        {
                            Generator = new Ob_StatisticalNameGenerator(),
                            Weight = 0.9f
                        },
                        new Ob_CompositeNameGeneratorEntry()
                        {
                            Generator = new Ob_ExoticNameGenerator(),
                            Weight = 0.1f
                        }
                    }
                });
                res.SessionComponents.Add(new Ob_DesignTools());
                // res.SessionComponents.Add(new Ob_ProceduralStation());
                res.SessionComponents.Add(new Ob_InfinitePlanets()
                {
                    SystemProbability = 0.5,
                    Systems = new List<Ob_InfinitePlanets_SystemDesc>()
                    {
                        new Ob_InfinitePlanets_SystemDesc()
                        {
                            MinDistanceFromOrigin = 0,
                            PlanetTypes = new List<Ob_InfinitePlanets_PlanetDesc>()
                            {
                                new Ob_InfinitePlanets_PlanetDesc()
                                {
                                    Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "EarthLike"),
                                    BodyRadius = new Ob_InfinitePlanets_Range() {Min = 100e3, Max = 120e3},
                                    OrbitRadius = new Ob_InfinitePlanets_Range() {Min = 500e3, Max = 1000e3},
                                    Probability = 2,
                                    MoonCount = new Ob_InfinitePlanets_Range() {Min = 1, Max = 1},
                                    MoonTypes = new List<Ob_InfinitePlanets_MoonDesc>()
                                    {
                                        new Ob_InfinitePlanets_MoonDesc()
                                        {
                                            Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "Moon"),
                                            BodyRadius = new Ob_InfinitePlanets_Range() {Min = 40e3, Max = 60e3},
                                            Probability = 1
                                        }
                                    }
                                },
                                new Ob_InfinitePlanets_PlanetDesc()
                                {
                                    Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "Mars"),
                                    BodyRadius = new Ob_InfinitePlanets_Range() {Min = 100e3, Max = 120e3},
                                    OrbitRadius = new Ob_InfinitePlanets_Range() {Min = 1500e3, Max = 2500e3},
                                    MoonCount = new Ob_InfinitePlanets_Range() {Min = 0, Max = 0},
                                    Probability = 2,
                                    MoonTypes = new List<Ob_InfinitePlanets_MoonDesc>()
                                    {
                                    }
                                },
                                new Ob_InfinitePlanets_PlanetDesc()
                                {
                                    Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "Alien"),
                                    BodyRadius = new Ob_InfinitePlanets_Range() {Min = 150e3, Max = 250e3},
                                    OrbitRadius = new Ob_InfinitePlanets_Range() {Min = 3000e3, Max = 6000e3},
                                    Probability = 1,
                                    MoonCount = new Ob_InfinitePlanets_Range() {Min = 3, Max = 6},
                                    MoonTypes = new List<Ob_InfinitePlanets_MoonDesc>()
                                    {
                                        new Ob_InfinitePlanets_MoonDesc()
                                        {
                                            Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "Europa"),
                                            BodyRadius = new Ob_InfinitePlanets_Range() {Min = 30e3, Max = 50e3},
                                            Probability = 1
                                        },
                                        new Ob_InfinitePlanets_MoonDesc()
                                        {
                                            Generator = new SerializableDefinitionId(typeof(MyObjectBuilder_PlanetGeneratorDefinition), "Titan"),
                                            BodyRadius = new Ob_InfinitePlanets_Range() {Min = 75e3, Max = 100e3},
                                            Probability = 1
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            return res;
        }

        public Settings Settings { get; }

        public SessionCore()
        {
            Settings = new Settings();
            Manager.RegisterFactory(LoggerBase.SuppliedDeps, () => new CustomLogger());
            Manager.RegisterFactory(CommandDispatchComponent.SuppliedDeps, () => new CommandDispatchComponent());
            Manager.RegisterFactory(NetworkComponent.SuppliedDeps, () => new NetworkComponent());
            Manager.RegisterFactory(RPCComponent.SuppliedDeps, () => new RPCComponent());
            Manager.RegisterFactory(ProceduralWorldManager.SuppliedDeps, () => new ProceduralWorldManager());
            Manager.RegisterFactory(PartManager.SuppliedDeps, () => new PartManager());
            Manager.RegisterFactory(ProceduralFactions.SuppliedDeps, () => new ProceduralFactions());
            Manager.RegisterFactory(StationGeneratorManager.SuppliedDeps, () => new StationGeneratorManager());
            Manager.RegisterFactory(BuildingDatabase.SuppliedDeps, () => new BuildingDatabase());
            Manager.RegisterFactory(NameGeneratorBase.SuppliedDeps, () =>
            {
                var gen = new CompositeNameGenerator();
                var config = new Ob_CompositeNameGenerator();
                config.Generators.Add(new Ob_CompositeNameGeneratorEntry()
                {
                    Generator = new Ob_StatisticalNameGenerator() {StatisticsDatabase = "res:english"},
                    Weight = 0.9f
                });
                config.Generators.Add(new Ob_CompositeNameGeneratorEntry()
                {
                    Generator = new Ob_ExoticNameGenerator(),
                    Weight = 0.1f
                });
                gen.LoadConfiguration(config);
                return gen;
            });
        }

        private bool LoadConfigFromFile()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage("session.xml", typeof(SessionCore)))
                {
                    using (var reader =
                        MyAPIGateway.Utilities.ReadFileInWorldStorage("session.xml",
                            typeof(SessionCore)))
                    {
                        var value =
                            MyAPIGateway.Utilities
                                .SerializeFromXML<MyObjectBuilder_SessionManager>(reader.ReadToEnd());
                        Manager.AppendConfiguration(value);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to parse config:\n{0}", e.ToString());
            }

            return false;
        }

        private bool LoadConfigFromModpack()
        {
            var prefab = MyDefinitionManager.Static.GetPrefabDefinition("EqProcWorldConfig");
            if (prefab == null)
                return false;
            foreach (var grid in prefab.CubeGrids)
            {
                foreach (var block in grid.CubeBlocks)
                {
                    var pbOb = block as MyObjectBuilder_MyProgrammableBlock;
                    var content = pbOb?.Program;
                    if (string.IsNullOrEmpty(content)) continue;
                    try
                    {
                        var data = Encoding.UTF8.GetString(Convert.FromBase64String(content));
                        var value =
                            MyAPIGateway.Utilities
                                .SerializeFromXML<MyObjectBuilder_SessionManager>(data);
                        Manager.AppendConfiguration(value);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to parse config inside PB {0} in grid {1}: {2}", pbOb.CustomName,
                            grid.DisplayName, e.Message);
                    }
                }
            }

            return false;
        }

        private bool m_init = false;

        public override void UpdateBeforeSimulation()
        {
            if (!m_init)
            {
                try
                {
                    Manager.Register(new SessionBootstrapper());
                    if (MyAPIGateway.Session.IsDecider())
                    {
                        if (!RELEASE || (!LoadConfigFromFile() && !LoadConfigFromModpack()))
                        {
                            Manager.AppendConfiguration(DefaultConfiguration());
                        }
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

        private ILoggingBase Logger => Manager.FallbackLogger;

        public override void Draw()
        {
            MyAPIGateway.Entities?.GetEntities(null, (x) =>
            {
                var component = x?.Components?.Get<ProceduralGridComponent>();
                component?.DebugDraw();
                return false;
            });
        }
    }
}