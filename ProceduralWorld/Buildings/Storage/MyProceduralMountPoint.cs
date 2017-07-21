using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Library;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    public class MyProceduralMountPoint
    {
        public MyProceduralRoom Owner { get; private set; }
        private string m_mountType, m_mountInstance;
        public MyPartMount MountPoint
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

        public MyProceduralMountPoint()
        {
            Owner = null;
            MountPoint = null;
        }

        private void Orphan()
        {
            Owner = null;
        }

        private void TakeOwnership(MyProceduralRoom parent)
        {
            Orphan();
            Owner = parent;
        }

        internal void Init(MyPartMount mount, MyProceduralRoom parent)
        {
            MountPoint = mount;
            TakeOwnership(parent);
        }

        public void Init(MyObjectBuilder_ProceduralMountPoint ob, MyProceduralRoom parent)
        {
            MountPoint = parent.Part.MountPoint(ob.TypeID, ob.InstanceID);
            TakeOwnership(parent);
        }

        public IEnumerable<Vector3I> MountLocations => MountPoint.Blocks.Select(x => Owner.PrefabToGrid(x.MountLocation));
        public IEnumerable<Vector3I> AnchorLocations => MountPoint.Blocks.Select(x => Owner.PrefabToGrid(x.AnchorLocation));

        public MyProceduralMountPoint AttachedToIn(MyProceduralConstruction construction)
        {
            foreach (var l in MountLocations)
            {
                var other = construction.MountPointAt(l);
                if (other != null) return other;
            }
            return null;
        }

        public MyProceduralMountPoint AttachedTo => Owner?.Owner != null ? AttachedToIn(Owner.Owner) : null;

        public override string ToString()
        {
            return $"ProceduralMount[{MountPoint.MountType}:{MountPoint.MountName} at {MountLocations.Aggregate("", (a, b) => b + ", " + a)}]";
        }
    }
}