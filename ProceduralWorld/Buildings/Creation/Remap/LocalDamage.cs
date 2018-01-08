using System.Collections.Generic;
using Equinox.Utils.Logging;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.Keen;
using Sandbox.Definitions;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation.Remap
{
    public class GridRemap_LocalDamage : IGridRemap
    {
        private int m_seed;
        private IMyModule m_localDamage = null;
        public int Seed
        {
            get
            {
                return m_seed;
            }
            set
            {
                m_localDamage = new MySimplexFast(value, 1.0 / 10.0);
                m_seed = value;
            }
        }

        public GridRemap_LocalDamage(ILoggingBase root) : base(root)
        {
            Seed = 0;
            DamageOffset = 0.5;
        }

        /// <summary>
        /// Shift applied to noise before damage occurs.  val = noise - DamageOffset.
        /// </summary>
        public double DamageOffset { get; set; }

        public override void Remap(MyObjectBuilder_CubeGrid grid)
        {
            if (m_localDamage == null) return;
            var removed = new List<Vector3I>();
            for (var i = 0; i < grid.CubeBlocks.Count; i++)
            {
                var cube = grid.CubeBlocks[i];
                var position = Vector3D.Transform((Vector3I)cube.Min * MyDefinitionManager.Static.GetCubeSize(grid.GridSizeEnum), grid.PositionAndOrientation?.GetMatrix() ?? MatrixD.Identity);
                var localDamage = m_localDamage.GetValue(position) - DamageOffset;
                localDamage *= localDamage * localDamage;
                if (localDamage < 0.2) continue;
                cube.IntegrityPercent = 1 - MyMath.Clamp((float)localDamage, 0, 1);
                if (cube.IntegrityPercent < 0.1)
                    removed.Add(cube.Min);
                else
                    grid.CubeBlocks[i - removed.Count] = cube;
            }
            if (removed.Count > 0)
                grid.CubeBlocks.RemoveRange(grid.CubeBlocks.Count - removed.Count, removed.Count);
        }

        public override void Reset()
        {
        }
    }
}
