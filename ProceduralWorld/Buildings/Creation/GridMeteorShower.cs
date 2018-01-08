using System;
using System.Collections.Generic;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Creation
{
    public class GridMeteorShower
    {
        public struct Meteor
        {
            public Vector3 Velocity;
            // [-1-1] shift from center.
            public Vector3 Shift;
            public double Radius;
            public double Mass;
        }
        private readonly List<Meteor> m_impactDirectionRadius = new List<Meteor>();
        public readonly ILogging Logger;
        public GridMeteorShower(ILoggingBase rootLogger, params Meteor[] impacts)
        {
            Logger = rootLogger.CreateProxy(GetType().Name);
            foreach (var f in impacts)
                m_impactDirectionRadius.Add(f);
        }
        public GridMeteorShower(IEnumerable<Meteor> impacts)
        {
            foreach (var f in impacts)
                m_impactDirectionRadius.Add(f);
        }

        public void Apply(IReadOnlyList<IMyCubeGrid> group)
        {
            var totalAABB = BoundingBoxD.CreateInvalid();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var grid in group)
                totalAABB = totalAABB.Include(grid.WorldAABB);
            var totalSphere = new BoundingSphereD(totalAABB.Center, totalAABB.HalfExtents.Length());
            foreach (var impact in m_impactDirectionRadius)
            {
                var speed = impact.Velocity.Length();
                var direction = (Vector3D)impact.Velocity / speed;
                Vector3D start, end;
                {
                    var rayOffset = totalAABB.HalfExtents * 0.8 * (Vector3D) impact.Shift;
                    // mag2(rayOffset + l*direction) == radius*radius
                    // (rayOffset + l*direction)*(rayOffset + l*direction)
                    // mag2(rayOffset) + 2*l*dot(direction, rayOffset) + l*l*mag2(direction)
                    // mag2(rayOffset) - (radius*radius) + 2*l*dot(direction, rayOffset) + l*l == 0
                    var c = rayOffset.LengthSquared() - totalSphere.Radius * totalSphere.Radius;
                    var b = 2 * Vector3D.Dot(direction, rayOffset);
                    const float a = 1;
                    var rad = b * b - 4 * a* c;
                    if (rad <= double.Epsilon) continue;
                    var lLow = (-b - Math.Sqrt(rad)) / (2 * a);
                    var lHigh = (-b + Math.Sqrt(rad)) / (2 * a);
                    start = totalSphere.Center + rayOffset + lLow * direction;
                    end = totalSphere.Center + rayOffset + lHigh * direction;
                }
                var ray = new RayD(start, direction);

                var bestHitLocation = default(Vector3D);
                var bestHitDistanceSquared = double.MaxValue;
                foreach (var grid in group)
                {
                    if (!grid.WorldAABB.Intersects(ray).HasValue) continue;
                    var block = grid.RayCastBlocks(start, end);
                    if (!block.HasValue) continue;
                    var world = Vector3D.Transform(block.Value * grid.GridSize, grid.WorldMatrix);
                    var distance = Vector3D.DistanceSquared(world, start);
                    if (distance > bestHitDistanceSquared) continue;
                    bestHitDistanceSquared = distance;
                    bestHitLocation = world;
                }
                if (bestHitDistanceSquared > double.MaxValue / 2) continue;
                var impactSphere = new BoundingSphereD(bestHitLocation, impact.Radius);
                var localSphere = new BoundingSphereD();
                var damageAmount = impact.Mass * speed * speed * (4.0 / 3.0) * Math.PI;
                var damageTotals = new Dictionary<IMySlimBlock, double>();
                foreach (var grid in group)
                    if (grid.WorldAABB.Intersects(impactSphere))
                    {
                        // compute local sphere.
                        localSphere.Center = Vector3D.Transform(impactSphere.Center, grid.WorldMatrixNormalizedInv) / grid.GridSize;
                        localSphere.Radius = impactSphere.Radius / grid.GridSize;
                        var min = Vector3I.Max(Vector3I.Floor(localSphere.Center - localSphere.Radius), grid.Min);
                        var max = Vector3I.Min(Vector3I.Ceiling(localSphere.Center + localSphere.Radius), grid.Max);
                        for (var itr = new Vector3I_RangeIterator(ref min, ref max); itr.IsValid(); itr.MoveNext())
                        {
                            if (localSphere.Contains(itr.Current) == ContainmentType.Disjoint) continue;
                            var block = grid.GetCubeBlock(itr.Current);
                            if (block == null) continue;
                            var distanceFactor = 1 - ((Vector3D)itr.Current - localSphere.Center).LengthSquared() / (localSphere.Radius * localSphere.Radius);
                            var blockDamage = damageAmount * distanceFactor * ((block.BlockDefinition as MyCubeBlockDefinition)?.DeformationRatio ?? 1);
                            damageTotals.AddValue(block, blockDamage);
                        }
                    }
                // No idea what shape key should be.
                Logger.Debug("Apply damage to {0} blocks", damageTotals.Count);
                var hitInfo = new MyHitInfo() { Normal = direction, Position = impactSphere.Center, Velocity = impact.Velocity, ShapeKey = 0 };
                foreach (var kv in damageTotals)
                    kv.Key.DoDamage((float)kv.Value, MyDamageType.Explosion, true, hitInfo);
            }
        }
    }
}
