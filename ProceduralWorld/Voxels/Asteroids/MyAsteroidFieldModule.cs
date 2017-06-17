using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Voxels.VoxelBuilder;
using Equinox.Utils.Logging;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.VRage;
using Equinox.Utils.Session;
using ProtoBuf;
using VRage;
using VRage.Collections;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public partial class MyAsteroidFieldModule : MyProceduralModule
    {
        public IMyAsteroidFieldShape Shape { get; private set; }
        public Matrix Transform
        {
            get { return m_transform; }
            set
            {
                m_transform = value;
                Matrix.Invert(ref m_transform, out m_invTransform);
            }
        }
        private Matrix m_transform, m_invTransform;
        private MyAsteroidLayer[] m_layers;
        private IMyModule m_noise = new MySimplexFast(1, 10);
        private IMyModule m_noiseWarp = new MySimplexFast(2, 4e-3);

        private readonly Dictionary<Vector4I, MyProceduralAsteroid> m_asteroids = new Dictionary<Vector4I, MyProceduralAsteroid>(Vector4I.Comparer);

        public override IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
        {
            var root = Vector3D.Transform(include.Center, m_invTransform);
            var excludeRoot = exclude.HasValue ? Vector3D.Transform(exclude.Value.Center, m_invTransform) : default(Vector3D);

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
                    var localPos = ((Vector3D)itr.Current + 0.5) * layer.AsteroidSpacing;

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
                    MyProceduralAsteroid procAst;
                    if (!m_asteroids.TryGetValue(seed, out procAst))
                    {
                        var size = m_noise.GetValue(worldPos) * (layer.AsteroidMaxSize - layer.AsteroidMinSize) + layer.AsteroidMinSize;
                        var genSeed = worldPos.GetHashCode();
                        genSeed = MyVoxelUtility.FindAsteroidSeed(genSeed, (float)size, m_layers[i].ProhibitsOre, m_layers[i].RequiresOre, 10);
                        m_asteroids[seed] = procAst = new MyProceduralAsteroid(this, seed, worldPos, size, genSeed);
                    }
                    procAst.SpawnIfNeeded((procAst.m_boundingBox.Center - include.Center).LengthSquared());
                    yield return procAst;
                }
            }
        }

        public override bool RunOnClients => true;


        private readonly MyBinaryStructHeap<MySpawnRequest, MySpawnRequest> m_asteroidsToAdd = new MyBinaryStructHeap<MySpawnRequest, MySpawnRequest>(128, MySpawnRequestComparer.Instance);
        private readonly MyQueue<MyProceduralAsteroid> m_asteroidsToRemove = new MyQueue<MyProceduralAsteroid>(128);

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
            //            Log(MyLogSeverity.Debug, "Spawning asteroid at {0}!", ast.m_boundingBox.Center);
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

        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent config)
        {
            var ob = config as MyObjectBuilder_AsteroidField;
            if (ob == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(), GetType());
                return;
            }

            Log(MyLogSeverity.Debug, "Loading configuration for {0}", GetType().Name);
            using (this.IndentUsing())
            {
                Transform = ob.Transform.GetMatrix();
                Log(MyLogSeverity.Debug, "Position is {0}", Transform.Translation);
                Log(MyLogSeverity.Debug, "Rotation is {0}", ob.Transform.Orientation);
                if (ob.ShapeSphere != null)
                {
                    Shape = new MyAsteroidSphereShape(ob.ShapeSphere);
                    Log(MyLogSeverity.Debug, "Shape is a sphere.  Radius is {0}m, thickness is {1}m", (ob.ShapeSphere.InnerRadius + ob.ShapeSphere.OuterRadius) / 2, ob.ShapeSphere.OuterRadius - ob.ShapeSphere.InnerRadius);
                    Log(MyLogSeverity.Debug, "Viewable at {0}", Vector3D.Transform(new Vector3D((ob.ShapeRing.OuterRadius + ob.ShapeRing.InnerRadius) / 2, 0, 0), Transform));
                }
                else if (ob.ShapeRing != null)
                {
                    Shape = new MyAsteroidRingShape(ob.ShapeRing);
                    Log(MyLogSeverity.Debug, "Shape is a ring.  Radius is {0}m, width is {1}m, height is {2}m", (ob.ShapeRing.InnerRadius + ob.ShapeRing.OuterRadius) / 2, ob.ShapeRing.OuterRadius - ob.ShapeRing.InnerRadius,
                        (ob.ShapeRing.OuterRadius - ob.ShapeRing.InnerRadius) * ob.ShapeRing.VerticalScaleMult);
                    Log(MyLogSeverity.Debug, "Viewable at {0}", Vector3D.Transform(new Vector3D((ob.ShapeRing.OuterRadius + ob.ShapeRing.InnerRadius) / 2, 0, 0), Transform));
                }
                else
                {
                    Log(MyLogSeverity.Debug, "Shape is unknown");
                    throw new ArgumentException();
                }
                Log(MyLogSeverity.Debug, "Seed is {0}", ob.Seed);
                m_layers = ob.Layers ?? new MyAsteroidLayer[0];
                for (var i = 0; i < m_layers.Length; i++)
                {
                    var layer = m_layers[i];
                    Log(MyLogSeverity.Debug, "Layer {0}", i + 1);
                    using (this.IndentUsing())
                    {
                        Log(MyLogSeverity.Debug, "Density = {0}", layer.AsteroidDensity);
                        Log(MyLogSeverity.Debug, "Asteroid size = {0} - {1}", layer.AsteroidMinSize, layer.AsteroidMaxSize);
                        Log(MyLogSeverity.Debug, "Spacing {0}", layer.AsteroidSpacing);
                        Log(MyLogSeverity.Debug, "Usable space {0}", layer.UsableRegion);
                        Log(MyLogSeverity.Debug, "Prohibited ores {0}", layer.ProhibitsOre.Aggregate("", (a, b) => b.SubtypeName + ", " + a));
                        Log(MyLogSeverity.Debug, "Required ores {0}", layer.RequiresOre.Aggregate("", (a, b) => b.SubtypeName + ", " + a));
                    }
                }
                m_noise = new MySimplexFast(ob.Seed, 10D);
                m_noiseWarp = new MySimplexFast(ob.Seed * 173, 10e-3);
            }
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            return new MyObjectBuilder_AsteroidField();
        }
    }

    public class MyObjectBuilder_AsteroidField : MyObjectBuilder_ModSessionComponent
    {
        [ProtoMember]
        [DefaultValue(null)]
        public MyAsteroidLayer[] Layers;

        [ProtoMember]
        public MyPositionAndOrientation Transform;

        [ProtoMember]
        public int Seed;

        [ProtoMember]
        public double DensityRegionSize = .1;

        [ProtoMember]
        [DefaultValue(null)]
        public MyObjectBuilder_AsteroidSphere ShapeSphere;

        [ProtoMember]
        [DefaultValue(null)]
        public MyObjectBuilder_AsteroidRing ShapeRing;
    }
}
