using System.Collections.Generic;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Voxels.Asteroids
{
    public class AutomaticAsteroidFieldsComponent : LoggingSessionComponent
    {
        private readonly Dictionary<MyDefinitionId, List<Ob_AsteroidField>> m_fieldsByPlanet = new Dictionary<MyDefinitionId, List<Ob_AsteroidField>>(MyDefinitionId.Comparer);

        private readonly Dictionary<long, List<AsteroidFieldModule>> m_fieldsByEntity = new Dictionary<long, List<AsteroidFieldModule>>();

        protected override void Attach()
        {
            base.Attach();
            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                OnEntityAdd(x);
                return false;
            });
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        private void OnEntityRemove(IMyEntity myEntity)
        {
            var planet = myEntity as MyPlanet;
            if (planet == null) return;
            List<AsteroidFieldModule> fields;
            if (!m_fieldsByEntity.TryGetValue(myEntity.EntityId, out fields))
                return;
            m_fieldsByEntity.Remove(myEntity.EntityId);
            foreach (var field in fields)
                Manager?.Unregister(field);
        }

        private void OnEntityAdd(IMyEntity myEntity)
        {
            var planet = myEntity as MyPlanet;
            if (planet == null) return;
            
            List<Ob_AsteroidField> fieldsHere;
            if (!m_fieldsByPlanet.TryGetValue(planet.Generator.Id, out fieldsHere))
                return;
            foreach (var field in fieldsHere)
            {
                var structure = new Ob_AsteroidField();
                structure.Seed = (int)(field.Seed ^ planet.EntityId);
                structure.Layers = new AsteroidLayer[field.Layers.Length];
                for (var i = 0; i < structure.Layers.Length; i++)
                {
                    var dst = structure.Layers[i] = new AsteroidLayer();
                    var src = field.Layers[i];
                    dst.AsteroidDensity = src.AsteroidDensity;
                    dst.AsteroidMaxSize = src.AsteroidMaxSize;
                    dst.AsteroidMinSize = src.AsteroidMinSize;
                    dst.AsteroidSpacing = src.AsteroidSpacing;
                    dst.UsableRegion = src.UsableRegion;
                    dst.ProhibitsOre.Clear();
                    foreach (var x in src.ProhibitsOre)
                        dst.ProhibitsOre.Add(x);
                    dst.RequiresOre.Clear();
                    foreach (var x in src.RequiresOre)
                        dst.RequiresOre.Add(x);
                }
                structure.ShapeRing = CloneUtilities.Clone(field.ShapeRing);
                structure.ShapeSphere = CloneUtilities.Clone(field.ShapeSphere);
                var rootTransform = MatrixD.CreateWorld(planet.PositionComp.WorldAABB.Center, planet.WorldMatrix.Forward, planet.WorldMatrix.Up);
                rootTransform = rootTransform * field.Transform;
                structure.Transform = rootTransform;
                var scalingFactor = (float) planet.PositionComp.WorldAABB.HalfExtents.Max();
                if (structure.ShapeRing != null)
                {
                    structure.ShapeRing.InnerRadius *= scalingFactor;
                    structure.ShapeRing.OuterRadius *= scalingFactor;
                }
                if (structure.ShapeSphere != null)
                {
                    structure.ShapeSphere.InnerRadius *= scalingFactor;
                    structure.ShapeSphere.OuterRadius *= scalingFactor;
                }

                var module = new AsteroidFieldModule();
                module.SaveToStorage = false;
                Log(MyLogSeverity.Debug, "Adding a new asteroid field for {0}", planet.Generator.Id);
                using (this.IndentUsing())
                {
                    Log(MyLogSeverity.Debug, "Planet radius is {0}", scalingFactor);
                }
                Manager?.Register(module, structure);

                List<AsteroidFieldModule> fields;
                if (!m_fieldsByEntity.TryGetValue(planet.EntityId, out fields))
                    fields = m_fieldsByEntity[planet.EntityId] = new List<AsteroidFieldModule>();
                fields.Add(module);
            }
        }

        protected override void Detach()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                OnEntityRemove(x);
                return false;
            });
            base.Detach();
        }

        public override void LoadConfiguration(Ob_ModSessionComponent config)
        {
            var up = config as Ob_AutomaticAsteroidFields;
            if (up == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", config.GetType(), GetType());
                return;
            }
            m_fieldsByPlanet.Clear();
            foreach (var x in up.AsteroidFields)
            {
                foreach (var k in x.OnPlanets)
                {
                    List<Ob_AsteroidField> fields;
                    if (!m_fieldsByPlanet.TryGetValue(k, out fields))
                        m_fieldsByPlanet[k] = fields = new List<Ob_AsteroidField>();
                    fields.Add(x.Field);
                }
            }
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            var reverse = new Dictionary<Ob_AsteroidField, List<MyDefinitionId>>();
            foreach (var kv in m_fieldsByPlanet)
                foreach (var field in kv.Value)
                {
                    List<MyDefinitionId> planets;
                    if (!reverse.TryGetValue(field, out planets))
                        planets = reverse[field] = new List<MyDefinitionId>();
                    planets.Add(kv.Key);
                }
            var result = new Ob_AutomaticAsteroidFields();
            foreach (var kv in reverse)
            {
                var temp = new Ob_AutoAsteroidField();
                foreach (var l in kv.Value)
                    temp.OnPlanets.Add(l);
                temp.Field = kv.Key;
                result.AsteroidFields.Add(temp);
            }
            return result;
        }
    }

    [ProtoContract]
    public class Ob_AutomaticAsteroidFields : Ob_ModSessionComponent
    {
        [ProtoMember]
        public List<Ob_AutoAsteroidField> AsteroidFields = new List<Ob_AutoAsteroidField>();
    }

    [ProtoContract]
    public class Ob_AutoAsteroidField
    {
        [ProtoMember]
        public List<SerializableDefinitionId> OnPlanets = new List<SerializableDefinitionId>();

        public Ob_AsteroidField Field;
    }
}
