using VRage.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Manager
{
    public class TrackedEntity
    {
        public readonly IMyEntity Entity;

        private double m_tolerance, m_toleranceSquared;

        public double Tolerance
        {
            get { return m_tolerance; }
            private set
            {
                m_tolerance = value;
                m_toleranceSquared = value * value;
            }
        }

        public double Radius
        {
            get { return m_previousView.Radius - Tolerance; }
            set
            {
                Tolerance = MathHelper.Clamp(value / 2, 128, 1024);
                m_previousView.Radius = value + Tolerance;
            }
        }

        public bool ShouldGenerate()
        {
            return !Entity.Closed && Entity.Save &&
                   (CurrentPosition - PreviousPosition).LengthSquared() > m_toleranceSquared;
        }

        private BoundingSphereD m_previousView, m_currentView;
        public Vector3D PreviousPosition => m_previousView.Center;
        public BoundingSphereD PreviousView => m_previousView;
        public Vector3D CurrentPosition => Entity.GetPosition();

        public BoundingSphereD CurrentView
        {
            get
            {
                m_currentView.Radius = m_previousView.Radius;
                m_currentView.Center = Entity.GetPosition();
                return m_currentView;
            }
        }

        public void UpdatePrevious()
        {
            m_previousView = CurrentView;
        }

        public TrackedEntity(IMyEntity entity)
        {
            Entity = entity;
            m_previousView.Center = Vector3D.MaxValue / 2;
        }
    }
}