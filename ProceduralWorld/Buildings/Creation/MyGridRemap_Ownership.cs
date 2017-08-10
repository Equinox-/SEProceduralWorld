using Equinox.Utils.Logging;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class MyGridRemap_Ownership : IMyGridRemap
    {
        public MyGridRemap_Ownership(IMyLoggingBase root) : base(root)
        {
        }

        public long? OwnerID { get; set; }
        public MyOwnershipShareModeEnum? ShareMode { get; set; }
        public bool UpgradeShareModeOnly { get; set; }

        public override void Remap(MyObjectBuilder_CubeGrid grid)
        {
            if (!OwnerID.HasValue && !ShareMode.HasValue) return;
            foreach (var block in grid.CubeBlocks)
            {
                if (OwnerID.HasValue)
                    block.Owner = OwnerID.Value;
                if (!ShareMode.HasValue) continue;
                if (!UpgradeShareModeOnly || block.ShareMode < ShareMode.Value)
                    block.ShareMode = ShareMode.Value;
            }
        }

        public override void Reset()
        {
        }
    }
}
