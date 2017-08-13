using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using Sandbox.Definitions;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public class MyPartManager : MyLoggingSessionComponent, IEnumerable<MyPartFromPrefab>
    {
        public const string PREFAB_NAME_PREFIX = "EqProcBuild_";

        private readonly Dictionary<MyDefinitionId, MyPartFromPrefab> m_parts = new Dictionary<MyDefinitionId, MyPartFromPrefab>(MyDefinitionId.Comparer);

        private List<MyPartFromPrefab> m_partsBySize;

        /// <summary>
        /// Gets a list of all parts, ascending by the volume of their bounding sphere
        /// </summary>
        public IEnumerable<MyPartFromPrefab> SortedBySize
        {
            get
            {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (m_partsBySize == null)
                    m_partsBySize = m_parts.Values.Where(x => x != null).OrderBy(x => x.BoundingBoxBoth.HalfExtents.LengthSquared()).ToList();
                return m_partsBySize;
            }
        }


        private static readonly Type[] SuppliedDeps = new[] { typeof(MyPartManager) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        protected override void Attach()
        {
            base.Attach();
            LoadAll();
        }

        protected override void Detach()
        {
            base.Detach();
            m_partsBySize = null;
            m_parts.Clear();
        }

        public void LoadAll()
        {
            var watch = new Stopwatch();
            watch.Restart();
            foreach (var def in MyDefinitionManager.Static.GetPrefabDefinitions())
                if (def.Value.Id.SubtypeName.StartsWithICase(PREFAB_NAME_PREFIX))
                    Load(def.Value);
            Log(MyLogSeverity.Info, "Loaded all prefabs ({0}) in {1}", m_parts.Count, watch.Elapsed);
        }

        public MyPartFromPrefab LoadNullable(SerializableDefinitionId prefabID)
        {
            MyPartFromPrefab part;
            if (m_parts.TryGetValue(prefabID, out part))
                return part;
            var def = MyDefinitionManager.Static.GetPrefabDefinition(prefabID.SubtypeId);
            return def != null ? Load(def) : null;
        }

        /// <summary>
        /// Retrieves or loads the given prefab as a multi-block part.
        /// </summary>
        /// <param name="def">The prefab to construct the part from</param>
        /// <param name="force">Force the part to be loaded immediately</param>
        /// <returns></returns>
        public MyPartFromPrefab Load(MyPrefabDefinition def, bool force = false)
        {
            MyPartFromPrefab part = null;
            if (!force && m_parts.TryGetValue(def.Id, out part)) return part;
            try
            {
                var output = new MyPartFromPrefab(this, def);
                if (force)
                    output.InitFromPrefab();
                // Can we actually use this with the current mods?
                MyCubeBlockDefinition test;
                foreach (var kv in output.BlockSetInfo.BlockCountByType)
                    if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(kv.Key, out test))
                    {
                        this.Info("Skipping prefab {0} since it uses block type {1} which is unknown",
                            def.Id.SubtypeName, kv.Key);
                        output = null;
                    }

                part = m_parts[def.Id] = output;
                if (output != null)
                {
                    m_partsBySize = null; // Invalidate sorted list.
                    foreach (var other in m_parts.Values)
                        foreach (var mount in other.MountPoints)
                            mount.InvalidateSmallestAttachment();
                }
            }
            catch (Exception e)
            {
                this.Error("Failed to load prefab {0}.\n{1}",def.Id.SubtypeName,  e);
            }
            return part;
        }

        public IEnumerator<MyPartFromPrefab> GetEnumerator()
        {
            return m_parts.Values.Where(x => x != null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_parts.Values.Where(x => x != null).GetEnumerator();
        }

        public MyPartFromPrefab this[MyDefinitionId key] => m_parts.GetValueOrDefault(key, null);


        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent config)
        {
            if (config == null) return;
            if (config is MyObjectBuilder_PartManager) return;
            Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(), GetType());
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            return new MyObjectBuilder_PartManager();
        }
    }

    public class MyObjectBuilder_PartManager : MyObjectBuilder_ModSessionComponent
    {
    }
}
