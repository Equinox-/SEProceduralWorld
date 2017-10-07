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
    public class MyBuildingDatabase : MyLoggingSessionComponent
    {
        private const string DatabaseFile = "buildings.xml";

        private readonly FastResourceLock m_lock = new FastResourceLock();
        private MyObjectBuilder_BuildingDatabase_Root m_root;

        public MyBuildingDatabase()
        {
            m_root = new MyObjectBuilder_BuildingDatabase_Root();
        }

        public static Type[] SuppliedDeps = { typeof(MyBuildingDatabase) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        public bool TryGetBuildingBlueprint(long seed, out MyObjectBuilder_ProceduralConstructionSeed seedResult, out MyObjectBuilder_ProceduralConstruction blueprint)
        {
            using (m_lock.AcquireSharedUsing())
            {
                var res = m_root?.Buildings.GetValueOrDefault(seed, null);
                seedResult = res?.Seed;
                blueprint = res?.Blueprint;
                return res != null;
            }
        }

        public bool TryGetFaction(ulong seed, out MyObjectBuilder_ProceduralFaction faction)
        {
            using (m_lock.AcquireSharedUsing())
            {
                faction = m_root?.Factions.GetValueOrDefault(seed, null);
                return faction != null;
            }
        }

        public void StoreFactionBlueprint(MyProceduralFactionSeed faction)
        {
            using (m_lock.AcquireExclusiveUsing())
                if (m_root != null)
                {
                    this.Info("Storing faction blueprint for {0}", faction.Name);
                    m_root.Factions[faction.Seed] = faction.GetObjectBuilder();
                }
        }

        public void StoreBuildingBlueprint(MyProceduralConstruction construction)
        {
            using (m_lock.AcquireExclusiveUsing())
                if (m_root != null && construction?.Seed != null)
                {
                    this.Info("Storing building blueprint for {0}", construction.Seed.Name);
                    m_root.Factions[construction.Seed.Faction.Seed] = construction.Seed.Faction.GetObjectBuilder();
                    m_root.Buildings[construction.Seed.Seed] =
                        new MyObjectBuilder_BuildingDatabase_Root.MyObjectBuilder_BuildingDatabase_BuildingNode()
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
                    if (MyAPIGateway.Utilities.FileExistsInWorldStorage(DatabaseFile, typeof(MyBuildingDatabase)))
                    {
                        m_root = null;
                        using (var reader =
                            MyAPIGateway.Utilities.ReadFileInWorldStorage(DatabaseFile, typeof(MyBuildingDatabase)))
                        {
                            m_root =
                                MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_BuildingDatabase_Root>(reader
                                    .ReadToEnd());
                        }
                    }
                }
                catch
                {
                    // ignore
                }
                if (m_root == null)
                    m_root = new MyObjectBuilder_BuildingDatabase_Root();
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
                        MyAPIGateway.Utilities.WriteFileInWorldStorage(DatabaseFile, typeof(MyBuildingDatabase)))
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

        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent configOriginal)
        {
            var config = configOriginal as MyObjectBuilder_BuildingDatabase;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            return new MyObjectBuilder_BuildingDatabase();
        }

        public class MyObjectBuilder_BuildingDatabase_Root
        {
            [XmlIgnore]
            public readonly Dictionary<long, MyObjectBuilder_BuildingDatabase_BuildingNode> Buildings = new Dictionary<long, MyObjectBuilder_BuildingDatabase_BuildingNode>();

            [XmlElement("Building")]
            public MyObjectBuilder_BuildingDatabase_BuildingNode[] SerialBuildings
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
            public readonly Dictionary<ulong, MyObjectBuilder_ProceduralFaction> Factions = new Dictionary<ulong, MyObjectBuilder_ProceduralFaction>();

            [XmlElement("Faction")]
            public MyObjectBuilder_ProceduralFaction[] SerialFactions
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

            public class MyObjectBuilder_BuildingDatabase_BuildingNode
            {
                [XmlElement("Seed")]
                public MyObjectBuilder_ProceduralConstructionSeed Seed;

                [XmlElement("Blueprint")]
                public MyObjectBuilder_ProceduralConstruction Blueprint;
            }
        }
    }

    public class MyObjectBuilder_BuildingDatabase : MyObjectBuilder_ModSessionComponent
    {
    }
}
