using System;
using System.Collections.Generic;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
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

        /// <summary>
        /// While technically you could change this at runtime... don't.
        /// </summary>
        public abstract bool RunOnClients { get; }

        protected override void Attach()
        {
            base.Attach();
            m_manager?.AddModule(this);
        }

        protected override void Detach()
        {
            base.Detach();
            m_manager?.RemoveModule(this);
        }
    }
}