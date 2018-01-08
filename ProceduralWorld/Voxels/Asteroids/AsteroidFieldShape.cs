using System;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public interface IAsteroidFieldShape
    {
        BoundingBoxD RelevantArea { get; }
        /// <summary>
        /// Calculates the shape's influence at the given location.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        double Weight(Vector3D location);
        Vector3 WarpSize { get; }
    }

    public class AsteroidRingShape : IAsteroidFieldShape
    {
        private readonly float m_innerSquared, m_outerSquared;
        private readonly float m_verticalSize;

        public AsteroidRingShape(Ob_AsteroidRing ob)
        {
            InnerRadius = ob.InnerRadius;
            OuterRadius = ob.OuterRadius;
            m_innerSquared = InnerRadius * InnerRadius;
            m_outerSquared = OuterRadius * OuterRadius;
            m_verticalSize = (OuterRadius - InnerRadius) * ob.VerticalScaleMult / 2;
            var extent = new Vector3D(ob.OuterRadius, m_verticalSize, ob.OuterRadius);
            RelevantArea = new BoundingBoxD(-extent, extent);
        }

        public float InnerRadius { get; }
        public float OuterRadius { get; }
        public float VerticalScaleMult => m_verticalSize * 2 / (OuterRadius - InnerRadius);

        public BoundingBoxD RelevantArea { get; }

        public double Weight(Vector3D location)
        {
            // Compute the distance to the torus's core.
            var magY = Math.Abs(location.Y);
            if (magY > m_verticalSize)
                return 0;
            var mag2 = (float)(location.X * location.X + location.Z * location.Z);
            if (mag2 < m_innerSquared || mag2 > m_outerSquared)
                return 0;

            var center = (InnerRadius + OuterRadius) / 2;
            var halfRad = (OuterRadius - InnerRadius) / 2;
            // (sqrt(mag2)-center)^2 + magY^2
            var planeDistance = (float)Math.Sqrt(mag2) - center;
            var xzHat = planeDistance / halfRad;
            var yHat = magY / m_verticalSize;
            var mag = Math.Sqrt(xzHat * xzHat + yHat * yHat);
            var distFromCenterNorm = MathHelper.Clamp(mag, 0, 1);
            return 1 - distFromCenterNorm * distFromCenterNorm;
        }

        public Vector3 WarpSize => new Vector3((OuterRadius - InnerRadius) * 0.1f);
    }

    public class AsteroidSphereShape : IAsteroidFieldShape
    {
        private readonly float m_innerSquared, m_outerSquared;

        public AsteroidSphereShape(Ob_AsteroidSphere ob)
        {
            RelevantArea = new BoundingBoxD(new Vector3D(-ob.OuterRadius), new Vector3D(ob.OuterRadius));
            InnerRadius = ob.InnerRadius;
            OuterRadius = ob.OuterRadius;
            m_innerSquared = InnerRadius * InnerRadius;
            m_outerSquared = OuterRadius * OuterRadius;
        }

        public float InnerRadius { get; }

        public float OuterRadius { get; }

        public BoundingBoxD RelevantArea { get; }

        public double Weight(Vector3D location)
        {
            var mag2 = (float)location.LengthSquared();
            if (mag2 < m_innerSquared || mag2 > m_outerSquared)
                return 0;
            float center, halfRad;
            if (InnerRadius < 1e-6 * OuterRadius)
            {
                // treat as sphere.
                center = 0;
                halfRad = OuterRadius - InnerRadius;
            }
            else
            {
                // treat as a shell
                center = (InnerRadius + OuterRadius) / 2;
                halfRad = (OuterRadius - InnerRadius) / 2;
            }
            var mag = Math.Abs((float)Math.Sqrt(mag2) - center);
            var distFromCenterNorm = MathHelper.Clamp(mag / halfRad, 0, 1);
            return 1 - distFromCenterNorm * distFromCenterNorm;
        }

        public Vector3 WarpSize => new Vector3((OuterRadius - InnerRadius) * 0.1f);
    }
}
