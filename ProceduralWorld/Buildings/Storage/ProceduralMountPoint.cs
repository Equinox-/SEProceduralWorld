using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Library;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    public class ProceduralMountPoint
    {
        public ProceduralRoom Owner { get; private set; }
        private string m_mountType, m_mountInstance;
        public PartMount MountPoint
        {
            get
            {
                return m_mountType != null && m_mountInstance != null ? Owner?.Part?.MountPoint(m_mountType, m_mountInstance) : null;
            }
            private set
            {
                m_mountType = value?.MountType;
                m_mountInstance = value?.MountName;
            }
        }

        public ProceduralMountPoint()
        {
            Owner = null;
            MountPoint = null;
        }

        private void Orphan()
        {
            Owner = null;
        }

        private void TakeOwnership(ProceduralRoom parent)
        {
            Orphan();
            Owner = parent;
        }

        internal void Init(PartMount mount, ProceduralRoom parent)
        {
            MountPoint = mount;
            TakeOwnership(parent);
        }

        public IEnumerable<Vector3I> MountLocations => MountPoint.Blocks.Select(x => Owner.PrefabToGrid(x.MountLocation));
        public IEnumerable<Vector3I> AnchorLocations => MountPoint.Blocks.Select(x => Owner.PrefabToGrid(x.AnchorLocation));

        public ProceduralMountPoint AttachedToIn(ProceduralConstruction construction)
        {
            foreach (var l in AnchorLocations)
            {
                var other = construction.MountPointAt(l);
                if (other != null) return other;
            }
            return null;
        }

        public ProceduralMountPoint AttachedTo => Owner?.Owner != null ? AttachedToIn(Owner.Owner) : null;

        public override string ToString()
        {
            return $"ProceduralMount[{MountPoint.MountType}:{MountPoint.MountName} at {string.Join(", ", MountLocations)}]";
        }
    }
}