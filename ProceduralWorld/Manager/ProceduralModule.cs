using System;
using System.Collections.Generic;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using VRageMath;

namespace Equinox.ProceduralWorld.Manager
{
    public abstract class ProceduralModule : LoggingSessionComponent
    {
        private ProceduralWorldManager m_manager;

        protected ProceduralModule()
        {
            DependsOn((ProceduralWorldManager x) =>
            {
                m_manager = x;
            });
        }

        public abstract IEnumerable<ProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude);

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