using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Voxels.VoxelBuilder;
using Equinox.Utils.Logging;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.Keen;
using Equinox.Utils.Session;
using ProtoBuf;
using Sandbox.Game.Components;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public partial class AsteroidFieldModule : ProceduralModule
    {
        public IAsteroidFieldShape Shape { get; private set; }

        public MatrixD Transform
        {
            get { return m_transform; }
            set
            {
                m_transform = value;
                MatrixD.Invert(ref m_transform, out m_invTransform);
            }
        }

        private MatrixD m_transform, m_invTransform;
        private AsteroidLayer[] m_layers;
        private IMyModule m_noise = new MySimplexFast(1, 10);
        private IMyModule m_noiseWarp = new MySimplexFast(2, 4e-3);
        private int m_seed;

        public int Seed
        {
            get { return m_seed; }
            set
            {
                m_seed = value;
                m_noise = new MySimplexFast(m_seed, 10D);
                m_noiseWarp = new MySimplexFast(m_seed * 173, 10e-3);
            }
        }

        private readonly Dictionary<Vector4I, ProceduralAsteroid> m_asteroids =
            new Dictionary<Vector4I, ProceduralAsteroid>(Vector4I.Comparer);

        public override IEnumerable<ProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
        {
            var root = Vector3D.Transform(include.Center, m_invTransform);
            var excludeRoot = exclude.HasValue
                ? Vector3D.Transform(exclude.Value.Center, m_invTransform)
                : default(Vector3D);

            var minLocal = root - include.Radius;
            var maxLocal = root + include.Radius;
            minLocal = Vector3D.Max(minLocal, Shape.RelevantArea.Min);
            maxLocal = Vector3D.Min(maxLocal, Shape.RelevantArea.Max);
            for (var i = 0; i < m_layers.Length; i++)
            {
                var layer = m_layers[i];
                var includePaddedSquared = include.Radius + layer.AsteroidSpacing * 2;
                includePaddedSquared *= includePaddedSquared;
                var excludePaddedSquared = exclude.HasValue ? exclude.Value.Radius - layer.AsteroidSpacing * 2 : 0;
                excludePaddedSquared *= excludePaddedSquared;

                var minPos = Vector3I.Floor(minLocal / layer.AsteroidSpacing);
                var maxPos = Vector3I.Ceiling(maxLocal / layer.AsteroidSpacing);
                for (var itr = new Vector3I_RangeIterator(ref minPos, ref maxPos); itr.IsValid(); itr.MoveNext())
                {
                    var seed = new Vector4I(itr.Current.X, itr.Current.Y, itr.Current.Z, i);
                    var localPos = ((Vector3D) itr.Current + 0.5) * layer.AsteroidSpacing;

                    // Very quick, include/exclude.
                    if (Vector3D.DistanceSquared(root, localPos) > includePaddedSquared) continue;
                    if (exclude.HasValue && Vector3D.DistanceSquared(excludeRoot, localPos) < excludePaddedSquared)
                        continue;
                    var localWeight = Shape.Weight(localPos) + layer.UsableRegion - 1;
                    if (1 - layer.AsteroidDensity > localWeight) continue;

                    var densityNoise = Math.Abs(m_noise.GetValue(localPos + Math.PI)) * localWeight;
                    if (1 - layer.AsteroidDensity > densityNoise) continue;

                    localPos.X += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;
                    localPos.Y += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;
                    localPos.Z += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;

                    localPos.X += 0.35 * Shape.WarpSize.X * m_noiseWarp.GetValue(localPos);
                    localPos.Y += 0.35 * Shape.WarpSize.Y * m_noiseWarp.GetValue(localPos);
                    localPos.Z += 0.05 * Shape.WarpSize.Z * m_noiseWarp.GetValue(localPos);

                    var worldPos = Vector3D.Transform(localPos, m_transform);
                    ProceduralAsteroid procAst;
                    if (!m_asteroids.TryGetValue(seed, out procAst))
                    {
                        var size = m_noise.GetValue(worldPos) * (layer.AsteroidMaxSize - layer.AsteroidMinSize) +
                                   layer.AsteroidMinSize;
                        m_asteroids[seed] = procAst = new ProceduralAsteroid(this, seed, worldPos, size, m_layers[i]);
                    }

                    procAst.SpawnIfNeeded((procAst.m_boundingBox.Center - include.Center).LengthSquared());
                    yield return procAst;
                }
            }
        }

        public override bool RunOnClients => true;


        private readonly MyBinaryStructHeap<SpawnRequest, SpawnRequest> m_asteroidsToAdd =
            new MyBinaryStructHeap<SpawnRequest, SpawnRequest>(128, SpawnRequestComparer.Instance);

        private readonly MyQueue<ProceduralAsteroid> m_asteroidsToRemove = new MyQueue<ProceduralAsteroid>(128);

        public override bool TickBeforeSimulationRoundRobin()
        {
            if (m_asteroidsToRemove.Count == 0) return false;
            m_asteroidsToRemove.Dequeue().ExecuteRemove();
            return true;
        }

        public override bool TickAfterSimulationRoundRobin()
        {
            if (m_asteroidsToAdd.Count == 0) return false;
            var ast = m_asteroidsToAdd.RemoveMax().Asteroid;
            ast.ExecuteSpawn();
            return true;
        }

        protected override void Detach()
        {
            m_asteroidsToAdd.Clear();
            foreach (var a in m_asteroids.Values)
                a.RaiseRemoved();
            m_asteroids.Clear();
            while (m_asteroidsToRemove.Count > 0)
                m_asteroidsToRemove.Dequeue().ExecuteRemove();
        }

        public override void LoadConfiguration(Ob_ModSessionComponent config)
        {
            var ob = config as Ob_AsteroidField;
            if (ob == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(),
                    GetType());
                return;
            }

            Log(MyLogSeverity.Debug, "Loading configuration for {0}", GetType().Name);
            using (this.IndentUsing())
            {
                Transform = ob.Transform;
                Log(MyLogSeverity.Debug, "Position is {0}", Transform.Translation);
                Log(MyLogSeverity.Debug, "Up is {0}", ob.Transform.Up);
                if (ob.ShapeSphere != null)
                {
                    Shape = new AsteroidSphereShape(ob.ShapeSphere);
                    Log(MyLogSeverity.Debug, "Shape is a sphere.  Radius is {0}m, thickness is {1}m",
                        (ob.ShapeSphere.InnerRadius + ob.ShapeSphere.OuterRadius) / 2,
                        ob.ShapeSphere.OuterRadius - ob.ShapeSphere.InnerRadius);
                    Log(MyLogSeverity.Debug, "Viewable at {0}",
                        Vector3D.Transform(
                            new Vector3D((ob.ShapeSphere.OuterRadius + ob.ShapeSphere.InnerRadius) / 2, 0, 0),
                            Transform));
                }
                else if (ob.ShapeRing != null)
                {
                    Shape = new AsteroidRingShape(ob.ShapeRing);
                    Log(MyLogSeverity.Debug, "Shape is a ring.  Radius is {0}m, width is {1}m, height is {2}m",
                        (ob.ShapeRing.InnerRadius + ob.ShapeRing.OuterRadius) / 2,
                        ob.ShapeRing.OuterRadius - ob.ShapeRing.InnerRadius,
                        (ob.ShapeRing.OuterRadius - ob.ShapeRing.InnerRadius) * ob.ShapeRing.VerticalScaleMult);
                    Log(MyLogSeverity.Debug, "Viewable at {0}",
                        Vector3D.Transform(
                            new Vector3D((ob.ShapeRing.OuterRadius + ob.ShapeRing.InnerRadius) / 2, 0, 0), Transform));
                }
                else
                {
                    Log(MyLogSeverity.Debug, "Shape is unknown");
                    throw new ArgumentException();
                }

                Log(MyLogSeverity.Debug, "Seed is {0}", ob.Seed);
                m_layers = ob.Layers ?? new AsteroidLayer[0];
                for (var i = 0; i < m_layers.Length; i++)
                {
                    var layer = m_layers[i];
                    Log(MyLogSeverity.Debug, "Layer {0}", i + 1);
                    using (this.IndentUsing())
                    {
                        Log(MyLogSeverity.Debug, "Density = {0}", layer.AsteroidDensity);
                        Log(MyLogSeverity.Debug, "Asteroid size = {0} - {1}", layer.AsteroidMinSize,
                            layer.AsteroidMaxSize);
                        Log(MyLogSeverity.Debug, "Spacing {0}", layer.AsteroidSpacing);
                        Log(MyLogSeverity.Debug, "Usable space {0}", layer.UsableRegion);
                        Log(MyLogSeverity.Debug, "Prohibited ores {0}", string.Join(", ", layer.ProhibitsOre));
                        Log(MyLogSeverity.Debug, "Required ores {0}", string.Join(", ", layer.RequiresOre));
                    }
                }

                Seed = ob.Seed;
            }
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            var field = new Ob_AsteroidField();
            field.Transform = Transform;
            var ring = Shape as AsteroidRingShape;
            var sphere = Shape as AsteroidSphereShape;
            if (ring != null)
            {
                field.ShapeRing = new Ob_AsteroidRing()
                {
                    InnerRadius = ring.InnerRadius,
                    OuterRadius = ring.OuterRadius,
                    VerticalScaleMult = ring.VerticalScaleMult
                };
            }
            else if (sphere != null)
            {
                field.ShapeSphere = new Ob_AsteroidSphere()
                {
                    InnerRadius = sphere.InnerRadius,
                    OuterRadius = sphere.OuterRadius
                };
            }

            field.Layers = m_layers;
            field.Seed = Seed;
            return field;
        }
    }
}