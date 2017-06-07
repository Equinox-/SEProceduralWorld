﻿using System;
using VRage;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class MyGridRemap_Coloring : IMyGridRemap
    {
        public SerializableVector3? OverrideColor { get; set; } = null;

        public float HueRotation { get; set; } = 0;
        public float SaturationModifier { get; set; } = 0;
        public float ValueModifier { get; set; } = 0;

        private void RemapColor(ref SerializableVector3 color)
        {
            if (OverrideColor != null)
            {
                color = OverrideColor.Value;
                return;
            }
            color.X = (color.X + HueRotation);
            color.X -= (float)Math.Floor(color.X); // make it [0,1]

            color.Y = MyMath.Clamp(color.Y * (SaturationModifier + 1), -1, 1);
            color.Z = MyMath.Clamp(color.Z * (ValueModifier + 1), -1, 1);
        }

        public void Remap(MyObjectBuilder_CubeGrid grid)
        {
            foreach (var block in grid.CubeBlocks)
                RemapColor(ref block.ColorMaskHSV);
        }

        public void Reset()
        {
        }
    }
}
