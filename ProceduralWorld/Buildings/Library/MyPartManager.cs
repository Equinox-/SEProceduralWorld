using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Utils.Session;
using Equinox.Utils;
using Sandbox.Definitions;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public class MyPartManager : MyLoggingSessionComponent, IEnumerable<MyPart>
    {
        public const string PREFAB_NAME_PREFIX = "EqProcBuild_";

        private readonly Dictionary<MyDefinitionId, MyPart> m_parts = new Dictionary<MyDefinitionId, MyPart>(MyDefinitionId.Comparer);

        private List<MyPart> m_partsBySize;

        /// <summary>
        /// Gets a list of all parts, ascending by the volume of their bounding sphere
        /// </summary>
        public IEnumerable<MyPart> SortedBySize
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
        public override IEnumerable<Type> SuppliesComponents => SuppliedDeps;

        public override void Attach()
        {
            base.Attach();
            LoadAll();
        }

        public override void Detach()
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

        public MyPart LoadNullable(SerializableDefinitionId prefabID)
        {
            MyPart part;
            if (m_parts.TryGetValue(prefabID, out part))
                return part;
            var def = MyDefinitionManager.Static.GetPrefabDefinition(prefabID.SubtypeId);
            return def != null ? Load(def) : null;
        }

        public MyPart Load(MyPrefabDefinition def)
        {
            MyPart part;
            if (m_parts.TryGetValue(def.Id, out part)) return part;
            try
            {
                var output = new MyPart(def);
                // Can we actually use this with the current mods?
                MyCubeBlockDefinition test;
                foreach (var kv in output.BlockSetInfo.BlockCountByType)
                    if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(kv.Key, out test))
                        output = null;

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
                SessionCore.LogBoth("Failed to load prefab {0}.  Cause:\n{1}", def.Id.SubtypeName, e);
            }
            return part;
        }

        public IEnumerator<MyPart> GetEnumerator()
        {
            return m_parts.Values.Where(x => x != null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_parts.Values.Where(x => x != null).GetEnumerator();
        }

        public MyPart this[MyDefinitionId key] => m_parts.GetValueOrDefault(key, null);
    }
}
