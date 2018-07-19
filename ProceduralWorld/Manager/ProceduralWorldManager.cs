using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Network;
using Equinox.Utils.Session;
using ProtoBuf;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender.Import;


// Inspiration taken from KSH's MyProceduralWorldGenerator.
namespace Equinox.ProceduralWorld.Manager
{
    public class ProceduralWorldManager : LoggingSessionComponent
    {
        public static readonly TimeSpan TolerableLag =
            TimeSpan.FromSeconds(MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS);

        public static readonly Type[] SuppliedDeps = {typeof(ProceduralWorldManager)};
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;

        private readonly HashSet<ProceduralModule> m_modules = new HashSet<ProceduralModule>();
        private readonly HashSet<ProceduralModule> m_modulesToAdd = new HashSet<ProceduralModule>();

        private readonly CachingDictionary<IMyEntity, TrackedEntity> m_trackedEntities =
            new CachingDictionary<IMyEntity, TrackedEntity>();

        private readonly MyQueue<BoundingSphereD> m_dirtyVolumes = new MyQueue<BoundingSphereD>(64);
        private readonly MyDynamicAABBTreeD m_tree = new MyDynamicAABBTreeD(new Vector3D(10));

        private readonly MyConcurrentPool<List<ProceduralObject>> m_objectListPool =
            new MyConcurrentPool<List<ProceduralObject>>(2);

        private void ObjectMoved(ProceduralObject item)
        {
            if (item.m_proxyID < 0) return;
            m_tree.MoveProxy(item.m_proxyID, ref item.m_boundingBox, Vector3D.Zero);
        }

        public void AddModule(ProceduralModule module)
        {
            if (m_modules.Contains(module) || m_modulesToAdd.Contains(module)) return;
            m_modulesToAdd.Add(module);
        }

        public void RemoveModule(ProceduralModule module)
        {
            m_modulesToAdd.Remove(module);
            if (!m_modules.Remove(module)) return;
            var list = m_objectListPool.Get();
            try
            {
                m_tree.GetAll(list, true);
                foreach (var x in list)
                    if (x.Module == module)
                        x.RaiseRemoved();
            }
            finally
            {
                m_objectListPool.Return(list);
            }
        }

        protected override void Attach()
        {
            MyAPIGateway.Entities.OnEntityAdd += TrackEntity;
            MyAPIGateway.Entities.GetEntities(null, x =>
            {
                TrackEntity(x);
                return false;
            });
            if (MyAPIGateway.Session.IsDecider() && MyAPIGateway.Session.Player?.Controller != null)
                TrackController(MyAPIGateway.Session.Player.Controller);
            Log(MyLogSeverity.Info, "Procedural world manager initialized");
        }

        protected override void Detach()
        {
            var list = m_objectListPool.Get();
            try
            {
                m_tree.GetAll(list, true);
                foreach (var x in list)
                    x.RaiseRemoved();
            }
            finally
            {
                m_objectListPool.Return(list);
            }
        }

        private void RemoveFromTree(ProceduralObject t)
        {
            if (t.m_proxyID == -1) return;
            m_tree.RemoveProxy(t.m_proxyID);
            t.m_proxyID = -1;
            t.OnMoved -= ObjectMoved;
            t.OnRemoved -= RemoveFromTree;
        }

        private bool AddToTree(ProceduralObject t)
        {
            if (t.m_proxyID != -1) return false;
            t.m_proxyID = m_tree.AddProxy(ref t.m_boundingBox, t, 0);
            t.OnMoved += ObjectMoved;
            t.OnRemoved += RemoveFromTree;
            return true;
        }

        private readonly Stopwatch m_stopwatch = new Stopwatch();

        public override void UpdateBeforeSimulation()
        {
            {
                m_trackedEntities.ApplyChanges();
                foreach (var module in m_modulesToAdd)
                    m_modules.Add(module);

                foreach (var module in m_modules)
                {
                    var needsFullUpdate = m_modulesToAdd.Remove(module);

                    if (!module.RunOnClients && !Utilities.IsDecisionMaker) return;
                    m_stopwatch.Restart();
                    foreach (var entity in m_trackedEntities.Values)
                    {
                        if (!entity.ShouldGenerate()) continue;
                        foreach (var result in module.Generate(entity.CurrentView,
                            needsFullUpdate ? null : (BoundingSphereD?) entity.PreviousView))
                            AddToTree(result);
                    }

                    var elapsed = m_stopwatch.Elapsed;
                    if (elapsed > TolerableLag)
                        Log(MyLogSeverity.Warning, "Module {0} took {1} to generate", module.GetType().Name, elapsed);
                }

                foreach (var entity in m_trackedEntities.Values)
                {
                    if (entity.ShouldGenerate())
                    {
                        m_dirtyVolumes.Enqueue(entity.PreviousView);
                        entity.UpdatePrevious();
                    }
                }
            }

            if (m_dirtyVolumes.Count == 0) return;
            var dirtyObjectList = m_objectListPool.Get();
            try
            {
                // Query tree for objects in volume.
                dirtyObjectList.Clear();
                while (m_dirtyVolumes.Count > 0)
                {
                    var volume = m_dirtyVolumes.Dequeue();
                    m_tree.OverlapAllBoundingSphere(ref volume, dirtyObjectList, false);
                }

                // Remove those not included by another entity
                foreach (var t in dirtyObjectList)
                {
                    bool any = false;
                    foreach (TrackedEntity entity in m_trackedEntities.Values)
                    {
                        if (t.m_boundingBox.Intersects(entity.CurrentView))
                        {
                            any = true;
                            break;
                        }
                    }

                    if (!any)
                        t.RaiseRemoved();
                }
            }
            finally
            {
                m_objectListPool.Return(dirtyObjectList);
            }
        }

        private void TrackController(IMyEntityController controller)
        {
            controller.ControlledEntityChanged += ControlledEntityChanged;
            var currEnt = controller.ControlledEntity as IMyEntity;
            if (currEnt != null)
                TrackEntity(currEnt);
        }

        private void TrackEntity(IMyEntity entity)
        {
            var player = entity as IMyCharacter;
            if (player != null)
            {
                if (MyAPIGateway.Session.IsDecider() || MyAPIGateway.Session.Player?.Controller == null)
                    TrackEntity(player, MyAPIGateway.Session.SessionSettings.ViewDistance);
                else
                {
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += ControlledEntityChanged;
                    var ctrl = player.ControllerInfo?.Controller;
                    if (MyAPIGateway.Session.Player == null || MyAPIGateway.Session.Player.Controller == ctrl)
                        TrackEntity(player, MyAPIGateway.Session.SessionSettings.ViewDistance);
                }
            }
            else if (entity is IMyCameraBlock)
                TrackEntity(entity, Math.Min(10000, MyAPIGateway.Session.SessionSettings.ViewDistance));
            else if (entity is IMyRemoteControl)
                TrackEntity(entity, Math.Min(10000, MyAPIGateway.Session.SessionSettings.ViewDistance));
        }

        private void ControlledEntityChanged(IMyControllableEntity old, IMyControllableEntity result)
        {
            var oldEnt = old as IMyEntity;
            var resultEnt = result as IMyEntity;
            if (oldEnt != null)
                RemoveEntity(oldEnt);
            if (resultEnt != null)
                TrackEntity(resultEnt, MyAPIGateway.Session.SessionSettings.ViewDistance);
        }

        private void RemoveEntity(IMyEntity x)
        {
            TrackedEntity tracker;
            if (!m_trackedEntities.TryGetValue(x, out tracker)) return;
            Log(MyLogSeverity.Debug, "Removing tracking for entity {0} ({1})", x, x.GetFriendlyName());
            m_dirtyVolumes.Enqueue(tracker.CurrentView);
            m_trackedEntities.Remove(x);
            x.OnMarkForClose -= RemoveEntity;
        }

        private void TrackEntity(IMyEntity entity, double distance)
        {
            Log(MyLogSeverity.Debug, "Track entity {0} ({2}) at {1} distance", entity, distance,
                entity.GetFriendlyName());
            TrackedEntity tracker;
            if (m_trackedEntities.TryGetValue(entity, out tracker))
                tracker.Radius = distance;
            else
            {
                m_trackedEntities[entity] = tracker = new TrackedEntity(entity);
                tracker.Radius = distance;
                entity.OnMarkForClose += RemoveEntity;
            }
        }

        public override void LoadConfiguration(Ob_ModSessionComponent config)
        {
            if (config == null) return;
            if (config is Ob_ProceduralWorldManager) return;
            Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(),
                GetType());
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_ProceduralWorldManager();
        }
    }

    [ProtoContract]
    public class Ob_ProceduralWorldManager : Ob_ModSessionComponent
    {
    }
}