using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;

namespace Equinox.ProceduralWorld.Buildings
{
    public class BuildingDatabase : LoggingSessionComponent
    {
        private const string DatabaseFile = "buildings.xml";

        private readonly FastResourceLock m_lock = new FastResourceLock();
        private Ob_BuildingDatabase_Root m_root;

        public BuildingDatabase()
        {
            m_root = new Ob_BuildingDatabase_Root();
        }

        public static Type[] SuppliedDeps = { typeof(BuildingDatabase) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        public bool TryGetBuildingBlueprint(long seed, out Ob_ProceduralConstructionSeed seedResult, out Ob_ProceduralConstruction blueprint)
        {
            using (m_lock.AcquireSharedUsing())
            {
                var res = m_root?.Buildings.GetValueOrDefault(seed, null);
                seedResult = res?.Seed;
                blueprint = res?.Blueprint;
                return res != null;
            }
        }

        public bool TryGetFaction(ulong seed, out Ob_ProceduralFaction faction)
        {
            using (m_lock.AcquireSharedUsing())
            {
                faction = m_root?.Factions.GetValueOrDefault(seed, null);
                return faction != null;
            }
        }

        public void StoreFactionBlueprint(ProceduralFactionSeed faction)
        {
            using (m_lock.AcquireExclusiveUsing())
                if (m_root != null)
                {
                    this.Info("Storing faction blueprint for {0}", faction.Name);
                    m_root.Factions[faction.Seed] = faction.GetObjectBuilder();
                }
        }

        public void StoreBuildingBlueprint(ProceduralConstruction construction)
        {
            using (m_lock.AcquireExclusiveUsing())
                if (m_root != null && construction?.Seed != null)
                {
                    this.Info("Storing building blueprint for {0}", construction.Seed.Name);
                    m_root.Factions[construction.Seed.Faction.Seed] = construction.Seed.Faction.GetObjectBuilder();
                    m_root.Buildings[construction.Seed.Seed] =
                        new Ob_BuildingDatabase_Root.Ob_BuildingDatabase_BuildingNode()
                        {
                            Blueprint = construction?.GetObjectBuilder(),
                            Seed = construction.Seed.GetObjectBuilder()
                        };
                }
        }

        protected override void Attach()
        {
            using (m_lock.AcquireExclusiveUsing())
            {
                try
                {
                    if (MyAPIGateway.Utilities.FileExistsInWorldStorage(DatabaseFile, typeof(BuildingDatabase)))
                    {
                        m_root = null;
                        using (var reader =
                            MyAPIGateway.Utilities.ReadFileInWorldStorage(DatabaseFile, typeof(BuildingDatabase)))
                        {
                            m_root =
                                MyAPIGateway.Utilities.SerializeFromXML<Ob_BuildingDatabase_Root>(reader
                                    .ReadToEnd());
                        }
                    }
                }
                catch
                {
                    // ignore
                }
                if (m_root == null)
                    m_root = new Ob_BuildingDatabase_Root();
            }
        }

        private readonly Stopwatch m_saveTimer = new Stopwatch();
        public override void Save()
        {
            m_saveTimer.Restart();
            using (m_lock.AcquireExclusiveUsing())
            {
                try
                {
                    var data = MyAPIGateway.Utilities.SerializeToXML(m_root);
                    using (var writer =
                        MyAPIGateway.Utilities.WriteFileInWorldStorage(DatabaseFile, typeof(BuildingDatabase)))
                        writer.Write(data);
                }
                catch (Exception e)
                {
                    this.Warning("Failed to save building database:\n{0}", e);
                }
            }
            this.Info("Saved building database ({0} buildings, {1} factions) in {2} seconds",
                m_root?.Buildings.Count ?? 0, m_root?.Factions.Count ?? 0, m_saveTimer.Elapsed.TotalSeconds);
        }

        protected override void Detach()
        {
            Save();
        }

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_BuildingDatabase;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_BuildingDatabase();
        }

        public class Ob_BuildingDatabase_Root
        {
            [XmlIgnore]
            public readonly Dictionary<long, Ob_BuildingDatabase_BuildingNode> Buildings = new Dictionary<long, Ob_BuildingDatabase_BuildingNode>();

            [XmlElement("Building")]
            public Ob_BuildingDatabase_BuildingNode[] SerialBuildings
            {
                get
                {
                    return Buildings.Values.ToArray();
                }
                set
                {
                    Buildings.Clear();
                    foreach (var k in value)
                        Buildings[k.Seed.Seed] = k;
                }
            }

            [XmlIgnore]
            public readonly Dictionary<ulong, Ob_ProceduralFaction> Factions = new Dictionary<ulong, Ob_ProceduralFaction>();

            [XmlElement("Faction")]
            public Ob_ProceduralFaction[] SerialFactions
            {
                get
                {
                    return Factions.Values.ToArray();
                }
                set
                {
                    Factions.Clear();
                    foreach (var k in value)
                        Factions[k.Seed] = k;
                }
            }

            public class Ob_BuildingDatabase_BuildingNode
            {
                [XmlElement("Seed")]
                public Ob_ProceduralConstructionSeed Seed;

                [XmlElement("Blueprint")]
                public Ob_ProceduralConstruction Blueprint;
            }
        }
    }

    public class Ob_BuildingDatabase : Ob_ModSessionComponent
    {
    }
}
