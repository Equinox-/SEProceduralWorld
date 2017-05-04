using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using VRage.Game;
using VRage.ObjectBuilders;

namespace ProcBuild
{
    internal class MyPartManager : IEnumerable<MyPart>
    {
        private readonly Dictionary<MyDefinitionId, MyPart> m_parts;

        public MyPartManager()
        {
            m_parts = new Dictionary<MyDefinitionId, MyPart>();
        }

        public void LoadAll()
        {
            foreach (var def in MyDefinitionManager.Static.GetPrefabDefinitions())
                if (def.Value.Id.SubtypeName.StartsWith("EqProcBuild_"))
                    Load(def.Value);
        }

        public MyPart LoadNullable(SerializableDefinitionId prefabID)
        {
            MyPart part;
            if (m_parts.TryGetValue(prefabID, out part))
                return part;
            MyPrefabDefinition def = MyDefinitionManager.Static.GetPrefabDefinition(prefabID.SubtypeId);
            if (def != null)
                return Load(def);
            return null;
        }

        public MyPart Load(MyPrefabDefinition def)
        {
            MyPart part;
            if (m_parts.TryGetValue(def.Id, out part)) return part;
            SessionCore.Log("Loading {0}", def.Id.SubtypeName);
            part = m_parts[def.Id] = new MyPart(def);
            SessionCore.Log("Loaded {0} with {1} mount points", def.Id.SubtypeName, part.MountPoints.Count());
            foreach (var type in part.MountPointTypes)
                SessionCore.Log("    ...of type \"{0}\" there are {1}", type, part.MountPointsOfType(type).Count());
            return part;
        }

        public IEnumerator<MyPart> GetEnumerator()
        {
            return m_parts.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_parts.Values.GetEnumerator();
        }

        public MyPart this[MyDefinitionId key] => m_parts[key];
    }
}
