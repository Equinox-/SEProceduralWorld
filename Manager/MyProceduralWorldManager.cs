using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Game;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;


// Inspiration taken from KSH's MyProceduralWorldGenerator.
namespace Equinox.ProceduralWorld.Manager
{
    public class MyTrackedEntity
    {
        public readonly IMyEntity Entity;

        private double m_tolerance, m_toleranceSquared;

        public double Tolerance
        {
            get
            {
                return m_tolerance;
            }
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
            return !Entity.Closed && Entity.Save && (CurrentPosition - PreviousPosition).LengthSquared() > m_toleranceSquared;
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

        public MyTrackedEntity(IMyEntity entity)
        {
            Entity = entity;
        }
    }

    public interface IMyProceduralModule
    {
        IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude);
        void UpdateBeforeSimulation();
    }

    public abstract class MyProceduralObject
    {
        internal int m_proxyID = -1;
        internal BoundingBoxD m_boundingBox = BoundingBoxD.CreateInvalid();

        public event Action<MyProceduralObject> OnMoved;

        public void RaiseMoved()
        {
            OnMoved?.Invoke(this);
        }

        public abstract void OnRemove();
    }

    public class MyProceduralWorldManager
    {
        private readonly List<IMyProceduralModule> m_modules = new List<IMyProceduralModule>();

        private readonly CachingDictionary<IMyEntity, MyTrackedEntity> m_trackedEntities = new CachingDictionary<IMyEntity, MyTrackedEntity>();
        private readonly CachingList<BoundingSphereD> m_dirtyVolumes = new CachingList<BoundingSphereD>();
        private readonly MyDynamicAABBTreeD m_tree = new MyDynamicAABBTreeD(new Vector3D(10));
        private readonly List<MyProceduralObject> m_dirtyObjects = new List<MyProceduralObject>();

        public void QueryOverlappingInSphere(BoundingSphereD sphere, List<MyProceduralObject> result, bool clear = false)
        {
            m_tree.OverlapAllBoundingSphere(ref sphere, result, clear);
        }

        public void QueryOverlappingInBoundingBox(BoundingBoxD box, List<MyProceduralObject> result, bool clear = false)
        {
            m_tree.OverlapAllBoundingBox(ref box, result, 0, clear);
        }

        private void ObjectMoved(MyProceduralObject item)
        {
            if (item.m_proxyID < 0) return;
            m_tree.MoveProxy(item.m_proxyID, ref item.m_boundingBox, Vector3D.Zero);
        }

        private static readonly TimeSpan TolerableLag = TimeSpan.FromSeconds(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS * 4);

        public void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null) return;
            // Server is the only person to spawn stuff.
            if (MyAPIGateway.Multiplayer != null && MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) return;

            if (!m_initialized)
                Init();

            var watch = new Stopwatch();
            foreach (var module in m_modules)
            {
                watch.Restart();
                module.UpdateBeforeSimulation();
                var elapsed = watch.Elapsed;
                if (elapsed > TolerableLag)
                    SessionCore.Log("Module {0} took {1} to update", module.GetType().Name, elapsed);
            }

            {
                m_trackedEntities.ApplyChanges();
                foreach (var module in m_modules)
                {
                    watch.Restart();
                    foreach (var entity in m_trackedEntities.Values)
                    {
                        if (!entity.ShouldGenerate()) continue;
                        foreach (var result in module.Generate(entity.CurrentView, entity.PreviousView))
                        {
                            if (!result.m_boundingBox.Intersects(entity.CurrentView))
                                SessionCore.Log("WARN: Generated AABB doesn't intersect view");
                            result.m_proxyID = m_tree.AddProxy(ref result.m_boundingBox, result, 0);
                            result.OnMoved += ObjectMoved;
                        }
                    }
                    var elapsed = watch.Elapsed;
                    if (elapsed > TolerableLag)
                        SessionCore.Log("Module {0} took {1} to generate", module.GetType().Name, elapsed);
                }
                foreach (var entity in m_trackedEntities.Values)
                {
                    m_dirtyVolumes.Add(entity.PreviousView); //meh, could be better.
                    entity.UpdatePrevious();
                }
            }

            m_dirtyVolumes.ApplyChanges();
            if (m_dirtyVolumes.Count == 0) return;
            {
                // Query tree for objects in volume.
                m_dirtyObjects.Clear();
                foreach (var volume in m_dirtyVolumes)
                {
                    var tmp = volume;
                    m_tree.OverlapAllBoundingSphere(ref tmp, m_dirtyObjects, false);
                }
                m_dirtyVolumes.Clear();

                // Remove those not included by another entity
                foreach (var t in m_dirtyObjects)
                    if (!m_trackedEntities.Values.Any(entity => t.m_boundingBox.Intersects(entity.CurrentView)))
                    {
                        m_tree.RemoveProxy(t.m_proxyID);
                        t.m_proxyID = -1;
                        t.OnMoved -= ObjectMoved;
                        t.OnRemove();
                    }
            }
        }

        public void Unload()
        {

        }

        private bool m_initialized = false;
        private void Init()
        {
            m_initialized = true;
            m_modules.Clear();
            m_modules.Add(new MyProceduralStationModule());
            MyAPIGateway.Entities.OnEntityAdd += TrackEntity;
            MyAPIGateway.Entities.GetEntities(null, x =>
            {
                TrackEntity(x);
                return false;
            });
            SessionCore.Log("Procedural world manager initialized");
        }

        // Track entity
        public void TrackEntity(IMyEntity entity)
        {
            if (entity is IMyCharacter)
                TrackEntity(entity, MyAPIGateway.Session.SessionSettings.ViewDistance);
            else if (entity is IMyCameraBlock)
                TrackEntity(entity, Math.Min(10000, MyAPIGateway.Session.SessionSettings.ViewDistance));
            else if (entity is IMyRemoteControl)
                TrackEntity(entity, Math.Min(10000, MyAPIGateway.Session.SessionSettings.ViewDistance));
        }

        private void TrackEntity(IMyEntity entity, double distance)
        {
            SessionCore.Log("Track entity {0} ({2}) at {1} distance", entity, distance, entity.GetFriendlyName());
            MyTrackedEntity tracker;
            if (m_trackedEntities.TryGetValue(entity, out tracker))
                tracker.Radius = distance;
            else
            {
                m_trackedEntities[entity] = tracker = new MyTrackedEntity(entity);
                tracker.Radius = distance;
                entity.OnMarkForClose += x =>
                {
                    SessionCore.Log("Removing tracking for entity {0} ({1})", x, x.GetFriendlyName());
                    m_dirtyVolumes.Add(tracker.CurrentView);
                    m_trackedEntities.Remove(x);
                };
            }
        }
    }
}
