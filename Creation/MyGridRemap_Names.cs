using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;

namespace ProcBuild.Creation
{
    public class MyGridRemap_Names : IMyGridRemap
    {
        public enum RemapType
        {
            ALL = 0,
            BLOCKS,
            GROUPS,
            /// <summary>
            /// Used to rename button labels on toolbars.
            /// </summary>
            LABELS,
            GRIDS
        }

        private readonly Dictionary<RemapType, string> m_prefix = new Dictionary<RemapType, string>();
        private readonly Dictionary<RemapType, string> m_suffix = new Dictionary<RemapType, string>();

        public string PrefixFor(RemapType type)
        {
            string val = null;
            return m_prefix.TryGetValue(type, out val) ? val : null;
        }

        public void PrefixFor(RemapType type, string prefix)
        {
            m_prefix[type] = prefix;
        }

        public string SuffixFor(RemapType type)
        {
            string val = null;
            return m_suffix.TryGetValue(type, out val) ? val : null;
        }

        public void SuffixFor(RemapType type, string suffix)
        {
            m_suffix[type] = suffix;
        }

        private void Remap(RemapType type, ref string current)
        {
            string prefix = null;
            if (!m_prefix.TryGetValue(type, out prefix))
                if (!m_prefix.TryGetValue(RemapType.ALL, out prefix))
                    prefix = null;
            string suffix = null;
            if (!m_suffix.TryGetValue(type, out suffix))
                if (!m_suffix.TryGetValue(RemapType.ALL, out suffix))
                    suffix = null;
            if (!string.IsNullOrWhiteSpace(prefix) && !current.StartsWith(prefix))
                current = prefix + current;
            if (!string.IsNullOrWhiteSpace(suffix) && !current.EndsWith(suffix))
                current = current + suffix;
        }

        private string Remap(RemapType type, string current)
        {
            Remap(type, ref current);
            return current;
        }

        public void Remap(MyObjectBuilder_CubeGrid grid)
        {
            if (grid.DisplayName != null)
                Remap(RemapType.GRIDS, ref grid.DisplayName);

            var toolbars = new List<MyObjectBuilder_Toolbar>();
            foreach (var block in grid.CubeBlocks)
            {
                var tblock = block as MyObjectBuilder_TerminalBlock;
                if (tblock?.CustomName != null)
                    Remap(RemapType.BLOCKS, ref tblock.CustomName);

                var buttonPanel = block as MyObjectBuilder_ButtonPanel;
                if (buttonPanel?.CustomButtonNames != null)
                    foreach (var k in buttonPanel.CustomButtonNames.Dictionary.Keys)
                        buttonPanel.CustomButtonNames[k] = Remap(RemapType.LABELS, buttonPanel.CustomButtonNames[k]);

                toolbars.Clear();
                toolbars.AddIfNotNull(buttonPanel?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_ShipController)?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_ShipController)?.BuildToolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_RemoteControl)?.AutoPilotToolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_TimerBlock)?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_SensorBlock)?.Toolbar);
                foreach (var toolbar in toolbars)
                    foreach (var s in toolbar.Slots)
                    {
                        var termGroup = s.Data as MyObjectBuilder_ToolbarItemTerminalGroup;
                        if (termGroup?.GroupName != null)
                            Remap(RemapType.GROUPS, ref termGroup.GroupName);
                    }
            }

            if (grid.BlockGroups != null)
                foreach (var group in grid.BlockGroups)
                    Remap(RemapType.GROUPS, ref group.Name);
        }

        // Name transform is deterministic, so we can skip storing any cache.
        public void Reset()
        {
        }
    }
}
