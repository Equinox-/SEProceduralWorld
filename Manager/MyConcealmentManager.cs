using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Collections;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Equinox.ProceduralWorld.Manager
{
    public static class MyConcealmentManager
    {
        private struct MyConcealedEntity
        {
            public bool PhysicsActive;
        }

        private static readonly Dictionary<long, MyConcealedEntity> ConcealedEntities = new Dictionary<long, MyConcealedEntity>();

        public static bool IsConcealed(this IMyEntity e)
        {
            lock (ConcealedEntities)
            {
                return ConcealedEntities.ContainsKey(e.EntityId);
            }
        }

        private static void OnClose(IMyEntity e)
        {
            lock (ConcealedEntities)
                ConcealedEntities.Remove(e.EntityId);
        }

        public static void SetConcealed(this IMyEntity e, bool conceal)
        {
            if (e.Hierarchy == null) return;
            MyConcealedEntity unconceal;
            lock (ConcealedEntities)
            {
                if (ConcealedEntities.TryGetValue(e.EntityId, out unconceal) == conceal) return;
                if (conceal)
                    ConcealedEntities.Add(e.EntityId, new MyConcealedEntity() { PhysicsActive = e.Physics?.IsActive ?? false });
                else
                    ConcealedEntities.Remove(e.EntityId);
            }
            if (conceal)
                e.OnClose += OnClose;
            else
                e.OnClose -= OnClose;

            foreach (var k in e.Hierarchy.Children)
                k.Entity?.SetConcealed(conceal);

#pragma warning disable 618
            if (conceal)
            {
                e.Physics?.Deactivate();
                MyAPIGateway.Entities.UnregisterForUpdate(e);
                if (e.Hierarchy.Parent == null)
                    e.RemoveFromGamePruningStructure();
            }
            else
            {
                if (e.Hierarchy.Parent == null)
                    e.AddToGamePruningStructure();
                MyAPIGateway.Entities.RegisterForUpdate(e);
                if (unconceal.PhysicsActive)
                    e.Physics?.Activate();
            }
#pragma warning restore 618
        }
    }
}
