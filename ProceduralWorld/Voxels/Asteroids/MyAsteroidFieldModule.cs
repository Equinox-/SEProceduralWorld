using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Equinox.ProceduralWorld.Manager;
using Equinox.ProceduralWorld.Voxels.Asteroids;
using Equinox.ProceduralWorld.Voxels.VoxelBuilder;
using Equinox.Utils.Noise;
using Equinox.Utils.Noise.VRage;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
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
        private readonly MyAsteroidLayer[] m_layers;
        private readonly IMyModule m_noise = new MySimplexFast(1, 10);
        private readonly IMyModule m_noiseWarp = new MySimplexFast(2, 4e-3);

        private readonly Dictionary<Vector4I, MyProceduralAsteroid> m_asteroids = new Dictionary<Vector4I, MyProceduralAsteroid>(Vector4I.Comparer);

        public override IEnumerable<MyProceduralObject> Generate(BoundingSphereD include, BoundingSphereD? exclude)
        {
            var minLocal = Vector3D.Transform(include.Center, m_invTransform) - include.Radius;
            var maxLocal = Vector3D.Transform(include.Center, m_invTransform) + include.Radius;
            minLocal = Vector3D.Max(minLocal, Shape.RelevantArea.Min);
            maxLocal = Vector3D.Min(maxLocal, Shape.RelevantArea.Max);

            for (var i = 0; i < m_layers.Length; i++)
            {
                var layer = m_layers[i];

                var minPos = Vector3I.Floor(minLocal / layer.AsteroidSpacing);
                var maxPos = Vector3I.Ceiling(maxLocal / layer.AsteroidSpacing);
                for (var itr = new Vector3I_RangeIterator(ref minPos, ref maxPos); itr.IsValid(); itr.MoveNext())
                {
                    var seed = new Vector4I(itr.Current.X, itr.Current.Y, itr.Current.Z, i);
                    var localPos = itr.Current * layer.AsteroidSpacing;
                    localPos += 0.5 * layer.AsteroidSpacing;
                    localPos.X += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;
                    localPos.Y += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;
                    localPos.Z += 0.45 * m_noise.GetValue(localPos) * layer.AsteroidSpacing;

                    var densityNoise = Math.Abs(m_noise.GetValue(localPos + 3.1415)) * Shape.Weight(localPos);
                    if (densityNoise > layer.AsteroidDensity) continue;

                    localPos.X += 0.35 * Shape.WarpSize.X * m_noiseWarp.GetValue(localPos);
                    localPos.Y += 0.35 * Shape.WarpSize.Y * m_noiseWarp.GetValue(localPos);
                    localPos.Z += 0.05 * Shape.WarpSize.Z * m_noiseWarp.GetValue(localPos);

                    var worldPos = Vector3D.Transform(localPos, m_transform);
                    if (include.Contains(worldPos) == ContainmentType.Disjoint) continue;
                    if (exclude.HasValue && exclude.Value.Contains(worldPos) != ContainmentType.Disjoint)
                        continue;

                    MyProceduralAsteroid procAst;
                    if (!m_asteroids.TryGetValue(seed, out procAst))
                    {
                        var size = m_noise.GetValue(worldPos) * (layer.AsteroidMaxSize - layer.AsteroidMinSize) + layer.AsteroidMinSize;
                        var genSeed = worldPos.GetHashCode();
                        genSeed = MyVoxelUtility.FindAsteroidSeed(genSeed, (float)size, m_layers[i].ProhibitsOre, m_layers[i].RequiresOre, 10);
                        m_asteroids[seed] = procAst = new MyProceduralAsteroid(this, seed, localPos, size, genSeed);
                    }
                    procAst.SpawnIfNeeded((procAst.m_boundingBox.Center - include.Center).LengthSquared());
                    yield return procAst;
                }
            }
        }

        public override bool RunOnClients => true;


        private readonly MyBinaryStructHeap<MySpawnRequest, MySpawnRequest> m_asteroidsToAdd = new MyBinaryStructHeap<MySpawnRequest, MySpawnRequest>(128, MySpawnRequestComparer.Instance);
        private readonly MyQueue<MyProceduralAsteroid> m_asteroidsToRemove = new MyQueue<MyProceduralAsteroid>(128);


        private int m_ticker = 0;
        private readonly Stopwatch m_loadTimer = new Stopwatch();
        public override void UpdateBeforeSimulation(TimeSpan maxTime)
        {
            if (m_ticker > 0)
            {
                m_ticker--;
                return;
            }
            m_loadTimer.Restart();
            while (m_loadTimer.Elapsed < maxTime && (m_asteroidsToAdd.Count != 0 || m_asteroidsToRemove.Count != 0))
            {
                if (m_asteroidsToAdd.Count > 0)
                {
                    m_ticker += 120;
                    m_asteroidsToAdd.RemoveMax().Asteroid?.ExecuteSpawn();
                    Log(MyLogSeverity.Debug, "Spawning asteroid!");
                }
                // ReSharper disable once InvertIf
                if (m_asteroidsToRemove.Count > 0)
                    m_asteroidsToRemove.Dequeue().ExecuteRemove();
            }
        }

        public override void Detach()
        {
            m_asteroidsToAdd.Clear();
            foreach (var a in m_asteroids.Values)
                a.RaiseRemoved();
            m_asteroids.Clear();
            while (m_asteroidsToRemove.Count > 0)
                m_asteroidsToRemove.Dequeue().ExecuteRemove();
        }

    }
}
