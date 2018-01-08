using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Creation.Remap
{
    public class GridRemap_Names : IGridRemap
    {
        public GridRemap_Names(ILoggingBase root) : base(root)
        {
        }

        public enum RemapType
        {
            All = 0,
            Blocks,
            Groups,
            /// <summary>
            /// Used to rename button labels on toolbars.
            /// </summary>
            Labels,
            Grids
        }

        private class RemapTypeEqualityComparer : IEqualityComparer<RemapType>
        {
            public static readonly RemapTypeEqualityComparer Instance = new RemapTypeEqualityComparer();
            public bool Equals(RemapType x, RemapType y)
            {
                return x == y;
            }

            public int GetHashCode(RemapType obj)
            {
                return (int)obj;
            }
        }

        private readonly Dictionary<RemapType, string> m_prefix = new Dictionary<RemapType, string>(RemapTypeEqualityComparer.Instance);
        private readonly Dictionary<RemapType, string> m_suffix = new Dictionary<RemapType, string>(RemapTypeEqualityComparer.Instance);

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
            string prefix = PrefixFor(type) ?? PrefixFor(RemapType.All);
            string suffix = SuffixFor(type) ?? SuffixFor(RemapType.All);
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

        public override void Remap(MyObjectBuilder_CubeGrid grid)
        {
            if (grid.DisplayName != null)
                Remap(RemapType.Grids, ref grid.DisplayName);

            var toolbars = new List<MyObjectBuilder_Toolbar>();
            foreach (var block in grid.CubeBlocks)
            {
                var tblock = block as MyObjectBuilder_TerminalBlock;
                if (tblock?.CustomName != null)
                    Remap(RemapType.Blocks, ref tblock.CustomName);

                var buttonPanel = block as MyObjectBuilder_ButtonPanel;
                if (buttonPanel?.CustomButtonNames != null)
                {
                    foreach (var k in buttonPanel.CustomButtonNames.Dictionary.Keys.ToArray())
                        buttonPanel.CustomButtonNames[k] = Remap(RemapType.Labels, buttonPanel.CustomButtonNames[k]);
                }

                toolbars.Clear();
                toolbars.AddIfNotNull(buttonPanel?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_ShipController)?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_ShipController)?.BuildToolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_RemoteControl)?.AutoPilotToolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_TimerBlock)?.Toolbar);
                toolbars.AddIfNotNull((block as MyObjectBuilder_SensorBlock)?.Toolbar);
                foreach (var toolbar in toolbars)
                {
                    foreach (var s in toolbar.Slots)
                    {
                        var termGroup = s.Data as MyObjectBuilder_ToolbarItemTerminalGroup;
                        if (termGroup?.GroupName != null)
                            Remap(RemapType.Groups, ref termGroup.GroupName);
                    }
                }
            }

            if (grid.BlockGroups != null)
                foreach (var group in grid.BlockGroups)
                    Remap(RemapType.Groups, ref group.Name);
        }

        // Name transform is deterministic, so we can skip storing any cache.
        public override void Reset()
        {
        }
    }
}
