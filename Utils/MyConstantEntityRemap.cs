using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.ModAPI;

namespace ProcBuild.Utils
{
    public class MyConstantEntityRemap : IMyRemapHelper
    {
        private readonly Dictionary<long, long> m_map;

        public MyConstantEntityRemap(IDictionary<long, long> map)
        {
            m_map = new Dictionary<long, long>(map);
        }

        public long RemapEntityId(long oldEntityId)
        {
            return m_map.GetValueOrDefault(oldEntityId, oldEntityId);
        }

        public int RemapGroupId(string @group, int oldValue)
        {
            return oldValue;
        }

        public void Clear()
        {
        }
    }
}
