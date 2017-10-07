using System;
using System.Collections.Generic;
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

    public class MySupportedBlockTypesEquality : IEqualityComparer<MySupportedBlockTypes>
    {
        public static readonly MySupportedBlockTypesEquality Instance = new MySupportedBlockTypesEquality();
        public bool Equals(MySupportedBlockTypes x, MySupportedBlockTypes y)
        {
            return x == y;
        }

        public int GetHashCode(MySupportedBlockTypes obj)
        {
            return (int)obj;
        }
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
                    return block is MyShipGrinderDefinition || block is MyShipWelderDefinition || block is MyProjectorDefinition;
                case MySupportedBlockTypes.Docking:
                    return block.Id.TypeId == typeof(MyObjectBuilder_ShipConnector);
                case MySupportedBlockTypes.Communications:
                    return block is MyLaserAntennaDefinition || block is MyRadioAntennaDefinition;
                default:
                    return false;
            }
        }
    }
}