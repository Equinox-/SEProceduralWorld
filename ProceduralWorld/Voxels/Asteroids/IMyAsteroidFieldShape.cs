using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public interface IMyAsteroidFieldShape
    {
        BoundingBoxD RelevantArea { get; }
        double Weight(Vector3D location);
        Vector3 WarpSize { get; }
    }
}
