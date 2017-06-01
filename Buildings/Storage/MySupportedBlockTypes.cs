using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    // Since we can't access `IsAssignableFrom` we've got to predefine all the types.
    public enum MySupportedBlockTypes
    {
        Weapon,
        CryoChamber,
        MedicalRoom,
        ShipController,
        ShipConstruction,
        Docking,
        Communications
    }

    public static class MySupportedBlockTypesExtension
    {
        public static bool IsOfType(this MySupportedBlockTypes type, MyDefinitionBase block)
        {
            switch (type)
            {
                case MySupportedBlockTypes.Weapon:
                    return block is MyWeaponBlockDefinition;
                case MySupportedBlockTypes.CryoChamber:
                    return block is MyCockpitDefinition && ((Type)block.Id.TypeId).Name.ToLower().Contains("cryo");
                case MySupportedBlockTypes.MedicalRoom:
                    return block is MyMedicalRoomDefinition;
                case MySupportedBlockTypes.ShipController:
                    return block is MyShipControllerDefinition;
                case MySupportedBlockTypes.ShipConstruction:
                    return block is MyShipGrinderDefinition || block is MyShipWelderDefinition;
                case MySupportedBlockTypes.Docking:
                    return block is MyMergeBlockDefinition || block.Id.TypeId == typeof(MyObjectBuilder_ShipConnector);
                case MySupportedBlockTypes.Communications:
                    return block is MyLaserAntennaDefinition || block is MyRadioAntennaDefinition;
                default:
                    return false;
            }
        }
    }
}