using System;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public interface IMyAsteroidFieldShape
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

    public class MyAsteroidRingShape : IMyAsteroidFieldShape
    {
        private readonly float m_inner, m_outer;
        private readonly float m_innerSquared, m_outerSquared;
        private readonly float m_verticalSize;

        public MyAsteroidRingShape(MyObjectBuilder_AsteroidRing ob)
        {
            m_inner = ob.InnerRadius;
            m_outer = ob.OuterRadius;
            m_innerSquared = m_inner * m_inner;
            m_outerSquared = m_outer * m_outer;
            m_verticalSize = (m_outer - m_inner) * ob.VerticalScaleMult / 2;
            var extent = new Vector3D(ob.OuterRadius, m_verticalSize, ob.OuterRadius);
            RelevantArea = new BoundingBoxD(-extent, extent);
        }

        public BoundingBoxD RelevantArea { get; }


        public double Weight(Vector3D location)
        {
            var magY = Math.Abs(location.Y);
            if (magY > m_verticalSize)
                return 0;
            var mag2 = (float)(location.X * location.X + location.Z * location.Z);
            if (mag2 < m_innerSquared || mag2 > m_outerSquared)
                return 0;

            var center = (m_inner + m_outer) / 2;
            var halfRad = (m_outer - m_inner) / 2;
            // (sqrt(mag2)-center)^2 + magY^2
            var planeDistance = (float)Math.Sqrt(mag2) - center;
            var xzHat = planeDistance / halfRad;
            var yHat = magY / m_verticalSize;
            var mag = Math.Sqrt(xzHat * xzHat + yHat * yHat);
            return 1 - MathHelper.Clamp(mag, 0, 1);
        }

        public Vector3 WarpSize => new Vector3((m_outer - m_inner) * 0.1f);
    }

    public class MyAsteroidSphereShape : IMyAsteroidFieldShape
    {
        private readonly float m_inner, m_outer;
        private readonly float m_innerSquared, m_outerSquared;

        public MyAsteroidSphereShape(MyObjectBuilder_AsteroidSphere ob)
        {
            RelevantArea = new BoundingBoxD(new Vector3D(-ob.OuterRadius), new Vector3D(ob.OuterRadius));
            m_inner = ob.InnerRadius;
            m_outer = ob.OuterRadius;
            m_innerSquared = m_inner * m_inner;
            m_outerSquared = m_outer * m_outer;
        }

        public BoundingBoxD RelevantArea { get; }

        public double Weight(Vector3D location)
        {
            var mag2 = (float)location.LengthSquared();
            if (mag2 < m_innerSquared || mag2 > m_outerSquared)
                return 0;
            float center, halfRad;
            if (m_inner < 1e-6 * m_outer)
            {
                // treat as sphere.
                center = 0;
                halfRad = m_outer - m_inner;
            }
            else
            {
                // treat as a shell
                center = (m_inner + m_outer) / 2;
                halfRad = (m_outer - m_inner) / 2;
            }
            var mag = Math.Abs((float)Math.Sqrt(mag2) - center);
            return 1 - MathHelper.Clamp(mag / halfRad, 0, 1);
        }

        public Vector3 WarpSize => new Vector3((m_outer - m_inner) * 0.1f);
    }
}
