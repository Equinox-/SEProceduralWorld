using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.Utils;
using Equinox.Utils.Noise;
using Equinox.Utils.Session;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public sealed partial class ProceduralStationModule : ProceduralModule
    {
        public OctreeNoise StationNoise { get; private set; }

        public ProceduralFactions Factions { get; private set; }
        public StationGeneratorManager Generator { get; private set; }
        private BuildingDatabase m_database;

        public static Type[] SuppliedDeps = {typeof(ProceduralStationModule)};
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        private void RebuildNoiseModules()
        {
            StationNoise = new OctreeNoise(ConfigReference.Seed, ConfigReference.StationMaxSpacing, ConfigReference.StationMinSpacing, null);
        }

        public ProceduralStationModule()
        {
            DependsOn<ProceduralFactions>(x => { Factions = x; });
            DependsOn<StationGeneratorManager>(x => { Generator = x; });
            DependsOn<BuildingDatabase>(x => { m_database = x; });
            LoadConfiguration(new Ob_ProceduralStation());
        }

        private readonly Dictionary<Vector4I, LoadingConstruction> m_instances = new Dictionary<Vector4I, LoadingConstruction>(Vector4I.Comparer);
        private readonly LinkedList<LoadingConstruction> m_dirtyInstances = new LinkedList<LoadingConstruction>();

        public override bool RunOnClients => false;

        public LoadingConstruction InstanceAt(Vector4I octreeNode)
        {
            return m_instances.GetValueOrDefault(octreeNode);
        }

        public LoadingConstruction InstanceAt(Vector3D worldPosition)
        {
            return InstanceAt(StationNoise.GetOctreeNodeAt(worldPosition));
        }

        public override IEnumerable<ProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
        {
            var aabb = new BoundingBoxD(include.Center - include.Radius, include.Center + include.Radius);
            foreach (var cell in StationNoise.TryGetSpawnIn(aabb, (x) => include.Intersects(x) && (!exclude.HasValue || exclude.Value.Contains(x) != ContainmentType.Contains)))
            {
                LoadingConstruction instance;
                if (!m_instances.TryGetValue(cell.Item1, out instance))
                {
                    var numSeed = cell.Item1.GetHashCode();
                    Ob_ProceduralConstructionSeed dbSeed;
                    Ob_ProceduralConstruction dbBlueprint;
                    Ob_ProceduralFaction dbFaction;

                    ProceduralConstructionSeed seed;
                    if (m_database.TryGetBuildingBlueprint(numSeed, out dbSeed, out dbBlueprint) && dbSeed != null &&
                        m_database.TryGetFaction(dbSeed.FactionSeed, out dbFaction) && dbFaction != null)
                    {
                        seed = new ProceduralConstructionSeed(new ProceduralFactionSeed(dbFaction), cell.Item2.XYZ(), dbSeed);
                    }
                    else
                    {
                        seed = new ProceduralConstructionSeed(Factions.SeedAt(cell.Item2.XYZ()), cell.Item2, null,
                            numSeed);
                    }
                    instance = m_instances[cell.Item1] = new LoadingConstruction(this, cell.Item1,
                        seed);
                }
                else if (!instance.IsMarkedForRemoval)
                    continue; // Already loaded + not marked for removal -- already in the tree.

                instance.EnsureGenerationStarted();
                yield return instance;
            }
        }

        public override void UpdateBeforeSimulation()
        {
            int hiddenEntities = 0, removedEntities = 0, removedOBs = 0, removedRecipes = 0;

            var node = m_dirtyInstances.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.TickRemoval(ref hiddenEntities, ref removedEntities, ref removedOBs, ref removedRecipes))
                    m_dirtyInstances.Remove(node);
                node = next;
            }
            if (removedEntities != 0 || removedOBs != 0 || removedRecipes != 0 || hiddenEntities != 0)
                Log(MyLogSeverity.Debug, "Procedural station module hide {3} station entities, removed {0} station entities, {1} object builders, and {2} recipes", removedEntities, removedOBs, removedRecipes, hiddenEntities);
        }

        public Ob_ProceduralStation ConfigReference { get; private set; }

        public override void LoadConfiguration(Ob_ModSessionComponent configBase)
        {
            var config = configBase as Ob_ProceduralStation;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}",
                    configBase.GetType(), GetType());
                return;
            }
            ConfigReference = config.Clone();
            RebuildNoiseModules();
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return ConfigReference.Clone();
        }
    }

    public class Ob_ProceduralStation : Ob_ModSessionComponent
    {
        internal new Ob_ProceduralStation Clone()
        {
            return MyAPIGateway.Utilities.SerializeFromXML<Ob_ProceduralStation>(MyAPIGateway.Utilities
                .SerializeToXML(this));
        }

        [ProtoMember]
        public long Seed = 1238721397L;

        // This is (within */2) of the minimum distance stations encounters are apart.  Keep high for performance reasons.
        // For context, 250e3 for Earth-Moon, 2300e3 for Earth-Mars, 6000e3 for Earth-Alien
        [ProtoMember]
        public double StationMinSpacing = 100e3;

        // This is the maximum distance station encounters are apart.
        [ProtoMember]
        public double StationMaxSpacing = 1000e3;

        // Procedural Station Management
        /// <summary>
        /// Time to keep a procedural station entity allocated after it's no longer visible.
        /// </summary>
        [XmlIgnore]
        public TimeSpan StationEntityPersistence = TimeSpan.FromMinutes(3);

        [ProtoMember]
        public double StationEntityPersistenceSeconds
        {
            get { return StationEntityPersistence.TotalSeconds; }
            set { StationEntityPersistence = TimeSpan.FromSeconds(value); }
        }

        /// <summary>
        /// Time to keep the object builder for a station object in memory after it's no longer visible.
        /// </summary>
        [XmlIgnore]
        public TimeSpan StationObjectBuilderPersistence = TimeSpan.FromMinutes(5);

        [ProtoMember]
        public double StationObjectBuilderPersistenceSeconds
        {
            get { return StationObjectBuilderPersistence.TotalSeconds; }
            set { StationObjectBuilderPersistence = TimeSpan.FromSeconds(value); }
        }

        /// <summary>
        /// Time to keep the high level structure of a station in memory after it's no longer visible.
        /// </summary>
        [XmlIgnore]
        public TimeSpan StationRecipePersistence = TimeSpan.FromMinutes(15);

        [ProtoMember]
        public double StationRecipePersistenceSeconds
        {
            get { return StationRecipePersistence.TotalSeconds; }
            set { StationRecipePersistence = TimeSpan.FromSeconds(value); }
        }
    }
}
