using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    // Since we can't access `IsAssignableFrom` we've got to predefine all the types.
    public enum SupportedBlockTypes
    {
        Weapon,
        CryoChamber,
        MedicalRoom,
        ShipController,
        ShipConstruction,
        Docking,
        Communications
    }

    public class SupportedBlockTypesEquality : IEqualityComparer<SupportedBlockTypes>
    {
        public static readonly SupportedBlockTypesEquality Instance = new SupportedBlockTypesEquality();
        public bool Equals(SupportedBlockTypes x, SupportedBlockTypes y)
        {
            return x == y;
        }

        public int GetHashCode(SupportedBlockTypes obj)
        {
            return (int)obj;
        }
    }

    public static class SupportedBlockTypesExtension
    {
        public static bool IsOfType(this SupportedBlockTypes type, MyDefinitionBase block)
        {
            switch (type)
            {
                case SupportedBlockTypes.Weapon:
                    return block is MyWeaponBlockDefinition;
                case SupportedBlockTypes.CryoChamber:
                    return block is MyCockpitDefinition && ((Type)block.Id.TypeId).Name.ToLower().Contains("cryo");
                case SupportedBlockTypes.MedicalRoom:
                    return block is MyMedicalRoomDefinition;
                case SupportedBlockTypes.ShipController:
                    return block is MyShipControllerDefinition;
                case SupportedBlockTypes.ShipConstruction:
                    return block is MyShipGrinderDefinition || block is MyShipWelderDefinition || block is MyProjectorDefinition;
                case SupportedBlockTypes.Docking:
                    return block.Id.TypeId == typeof(MyObjectBuilder_ShipConnector);
                case SupportedBlockTypes.Communications:
                    return block is MyLaserAntennaDefinition || block is MyRadioAntennaDefinition;
                default:
                    return false;
            }
        }
    }
}