using System;
using System.Collections.Generic;
using Equinox.ProceduralWorld.Utils.Session;
using VRageMath;

namespace Equinox.ProceduralWorld.Manager
{
    public abstract class MyProceduralModule : MyLoggingSessionComponent
    {
        private MyProceduralWorldManager m_manager;

        protected MyProceduralModule()
        {
            DependsOn((MyProceduralWorldManager x) =>
            {
                m_manager = x;
            });
        }

        public abstract IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude);
        public abstract void UpdateBeforeSimulation(TimeSpan maxTime);
        public abstract bool RunOnClients { get; }

        public override void Attach()
        {
            base.Attach();
            m_manager?.AddModule(this);
        }

        public override void Detach()
        {
            base.Detach();
            m_manager?.RemoveModule(this);
        }
    }
}