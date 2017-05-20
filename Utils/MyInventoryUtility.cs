using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Utils;

namespace ProcBuild.Utils
{
    public static class MyInventoryUtility
    {
        public static double GetInventoryVolume(MyDefinitionId id)
        {
            return CacheInvVolume.GetOrCreate(id, GetInventoryVolumeInternal);
        }
        private static readonly MyLRUCache<MyDefinitionId, double> CacheInvVolume = new MyLRUCache<MyDefinitionId, double>(128);

        private static double GetInventoryVolumeInternal(MyDefinitionId id)
        {
            MyContainerDefinition container;
            if (MyComponentContainerExtension.TryGetContainerDefinition(id.TypeId, id.SubtypeId, out container) && container.DefaultComponents != null)
                foreach (var component in container.DefaultComponents)
                {
                    MyComponentDefinitionBase componentDefinition = null;
                    if (!MyComponentContainerExtension.TryGetComponentDefinition(component.BuilderType, 
                        component.SubtypeId ?? id.SubtypeId, out componentDefinition)) continue;
                    var invDef = componentDefinition as MyInventoryComponentDefinition;
                    if (invDef != null)
                        return invDef.Volume * 1000;
                }

            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(id);
            if (def is MyCargoContainerDefinition)
                return (def as MyCargoContainerDefinition).InventorySize.Volume * 1000;
            return 0.0;
        }
    }
}
