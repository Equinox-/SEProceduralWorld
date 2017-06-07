using System;
using VRageMath;

namespace Equinox.ProceduralWorld.Manager
{
    public abstract class MyProceduralObject
    {
        public MyProceduralModule Module { get; }

        protected MyProceduralObject(MyProceduralModule caller)
        {
            Module = caller;
        }

        internal int m_proxyID = -1;
        internal BoundingBoxD m_boundingBox = BoundingBoxD.CreateInvalid();

        public event Action<MyProceduralObject> OnMoved;
        public event Action<MyProceduralObject> OnRemoved;

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