using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace ProcBuild
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class SessionCore : MySessionComponentBase
    {
        public override void LoadData()
        {
            base.LoadData();
        }

        private bool m_attached = false;

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (MyAPIGateway.Session == null) return;
            if (!m_attached)
                Attach();
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (m_attached)
                Detach();
        }

        private void Attach()
        {
            m_attached = true;

            MyAPIGateway.Utilities.MessageEntered += CommandDispatcher;
            PartManage();
        }

        private void Detach()
        {
            m_attached = false;

            MyAPIGateway.Utilities.MessageEntered -= CommandDispatcher;
        }

        private void CommandDispatcher(string messageText, ref bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer || !messageText.StartsWith("/")) return;
            if (messageText.StartsWith("/list"))
            {
                var prefabs = MyDefinitionManager.Static.GetPrefabDefinitions();
                foreach (var fab in prefabs)
                {
                    MyAPIGateway.Utilities.ShowMessage("ProcBuild", fab.Key);
                }
            }
        }

        private void PartManage()
        {
            var prefabs = MyDefinitionManager.Static.GetPrefabDefinitions();
            foreach (var fab in prefabs)
            {
            }
        }
    }
}
