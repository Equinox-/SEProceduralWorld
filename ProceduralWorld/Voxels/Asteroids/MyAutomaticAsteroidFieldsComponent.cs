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
    public class MyAutomaticAsteroidFieldsComponent : MyLoggingSessionComponent
    {
        private readonly Dictionary<MyDefinitionId, List<MyObjectBuilder_AsteroidField>> m_fieldsByPlanet = new Dictionary<MyDefinitionId, List<MyObjectBuilder_AsteroidField>>(MyDefinitionId.Comparer);

        private readonly Dictionary<long, List<MyAsteroidFieldModule>> m_fieldsByEntity = new Dictionary<long, List<MyAsteroidFieldModule>>();

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
            List<MyAsteroidFieldModule> fields;
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
            
            List<MyObjectBuilder_AsteroidField> fieldsHere;
            if (!m_fieldsByPlanet.TryGetValue(planet.Generator.Id, out fieldsHere))
                return;
            foreach (var field in fieldsHere)
            {
                var structure = new MyObjectBuilder_AsteroidField();
                structure.Seed = (int)(field.Seed ^ planet.EntityId);
                structure.Layers = new MyAsteroidLayer[field.Layers.Length];
                for (var i = 0; i < structure.Layers.Length; i++)
                {
                    var dst = structure.Layers[i] = new MyAsteroidLayer();
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
                structure.ShapeRing = MyCloneUtilities.Clone(field.ShapeRing);
                structure.ShapeSphere = MyCloneUtilities.Clone(field.ShapeSphere);
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

                var module = new MyAsteroidFieldModule();
                module.SaveToStorage = false;
                Log(MyLogSeverity.Debug, "Adding a new asteroid field for {0}", planet.Generator.Id);
                using (this.IndentUsing())
                {
                    Log(MyLogSeverity.Debug, "Planet radius is {0}", scalingFactor);
                }
                Manager?.Register(module, structure);

                List<MyAsteroidFieldModule> fields;
                if (!m_fieldsByEntity.TryGetValue(planet.EntityId, out fields))
                    fields = m_fieldsByEntity[planet.EntityId] = new List<MyAsteroidFieldModule>();
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

        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent config)
        {
            var up = config as MyObjectBuilder_AutomaticAsteroidFields;
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
                    List<MyObjectBuilder_AsteroidField> fields;
                    if (!m_fieldsByPlanet.TryGetValue(k, out fields))
                        m_fieldsByPlanet[k] = fields = new List<MyObjectBuilder_AsteroidField>();
                    fields.Add(x.Field);
                }
            }
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            var reverse = new Dictionary<MyObjectBuilder_AsteroidField, List<MyDefinitionId>>();
            foreach (var kv in m_fieldsByPlanet)
                foreach (var field in kv.Value)
                {
                    List<MyDefinitionId> planets;
                    if (!reverse.TryGetValue(field, out planets))
                        planets = reverse[field] = new List<MyDefinitionId>();
                    planets.Add(kv.Key);
                }
            var result = new MyObjectBuilder_AutomaticAsteroidFields();
            foreach (var kv in reverse)
            {
                var temp = new MyObjectBuilder_AutoAsteroidField();
                foreach (var l in kv.Value)
                    temp.OnPlanets.Add(l);
                temp.Field = kv.Key;
                result.AsteroidFields.Add(temp);
            }
            return result;
        }
    }

    [ProtoContract]
    public class MyObjectBuilder_AutomaticAsteroidFields : MyObjectBuilder_ModSessionComponent
    {
        [ProtoMember]
        public List<MyObjectBuilder_AutoAsteroidField> AsteroidFields = new List<MyObjectBuilder_AutoAsteroidField>();
    }

    [ProtoContract]
    public class MyObjectBuilder_AutoAsteroidField
    {
        [ProtoMember]
        public List<SerializableDefinitionId> OnPlanets = new List<SerializableDefinitionId>();

        public MyObjectBuilder_AsteroidField Field;
    }
}
