using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcBuild.Utils.Noise
{
    public class OctaveNoise
    {
        public static float Generate(float x, int octaves)
        {
            var accum = 0.0f;
            var div = 1.0f;
            var amp = 1.0f;
            for (var i = 0; i < octaves; i++)
            {
                accum += amp * SimplexNoise.Generate(x / div);
                div *= 1.997f;
                amp *= 1.997f;
            }
            return accum;
        }

        public static float Generate(float x, float y, int octaves)
        {
            var accum = 0.0f;
            var div = 1.0f;
            var amp = 1.0f;
            for (var i = 0; i < octaves; i++)
            {
                accum += amp * SimplexNoise.Generate(x / div, y / div);
                div *= 1.997f;
                amp *= 1.997f;
            }
            return accum;
        }

        public static float Generate(float x, float y, float z, int octaves)
        {
            var accum = 0.0f;
            var div = 1.0f;
            var amp = 1.0f;
            for (var i = 0; i < octaves; i++)
            {
                accum += amp * SimplexNoise.Generate(x / div, y / div, z / div);
                div *= 1.997f;
                amp *= 1.997f;
            }
            return accum;
        }
    }
}
