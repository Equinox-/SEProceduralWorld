using System;
using VRageMath;

namespace Equinox.ProceduralWorld.Manager
{
    public abstract class ProceduralObject
    {
        public ProceduralModule Module { get; }

        protected ProceduralObject(ProceduralModule caller)
        {
            Module = caller;
        }

        internal int m_proxyID = -1;
        internal BoundingBoxD m_boundingBox = BoundingBoxD.CreateInvalid();

        public event Action<ProceduralObject> OnMoved;
        public event Action<ProceduralObject> OnRemoved;

        public void RaiseMoved()
        {
            OnMoved?.Invoke(this);
        }

        public void RaiseRemoved()
        {
            OnRemoved?.Invoke(this);
        }
    }
}