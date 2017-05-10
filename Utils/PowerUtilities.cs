using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage;
using VRage.Utils;

namespace ProcBuild.Utils
{
    public class PowerUtilities
    {
        // Positive=consumption, negative=production
        private static MyTuple<MyStringHash, float> MaxPowerConsumptionInternal(MyCubeBlockDefinition def)
        {
            // refinery, blast furnace, assembler, oxygen tanks, hydrogen tanks, oxygen generator
            if (def is MyProductionBlockDefinition)
            {
                var v = (MyProductionBlockDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, v.OperationalPowerConsumption);
            }
            // thrusters
            if (def is MyThrustDefinition)
            {
                var v = (MyThrustDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, v.FuelConverter == null ? v.MaxPowerConsumption : 0);
            }
            // gyros
            if (def is MyGyroDefinition)
            {
                var v = (MyGyroDefinition)def;
                return MyTuple.Create(MyStringHash.GetOrCompute(v.ResourceSinkGroup), v.RequiredPowerInput);
            }
            // reactors
            if (def is MyReactorDefinition)
            {
                var v = (MyReactorDefinition)def;
                return MyTuple.Create(v.ResourceSourceGroup, -v.MaxPowerOutput);
            }
            // solar panels
            if (def is MySolarPanelDefinition)
            {
                var v = (MySolarPanelDefinition)def;
                return MyTuple.Create(v.ResourceSourceGroup, -v.MaxPowerOutput);
            }
            // lights
            if (def is MyLightingBlockDefinition)
            {
                var v = (MyLightingBlockDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, v.RequiredPowerInput);
            }
            if (def is MyAirVentDefinition)
            {
                var v = (MyAirVentDefinition) def;
                return MyTuple.Create(v.ResourceSinkGroup, v.OperationalPowerConsumption);
            }
            if (def is MyLaserAntennaDefinition)
            {
                var v = (MyLaserAntennaDefinition) def;
                return MyTuple.Create(v.ResourceSinkGroup, v.PowerInputLasing);
            }
            if (def is MyRadioAntennaDefinition)
            {
                var v = (MyRadioAntennaDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, MyEnergyConstants.MAX_REQUIRED_POWER_ANTENNA);
            }
            if (def is MyLargeTurretBaseDefinition)
            {
                var v = (MyLargeTurretBaseDefinition) def;
                return MyTuple.Create(v.ResourceSinkGroup, MyEnergyConstants.MAX_REQUIRED_POWER_TURRET);
            }
            if (def is MyOreDetectorDefinition)
            {
                var v = (MyOreDetectorDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, MyEnergyConstants.MAX_REQUIRED_POWER_ORE_DETECTOR);
            }
            if (def is MyBeaconDefinition)
            {
                var v = (MyBeaconDefinition)def;
                return MyTuple.Create(MyStringHash.GetOrCompute(v.ResourceSinkGroup), MyEnergyConstants.MAX_REQUIRED_POWER_BEACON);
            }
            if (def is MyDoorDefinition)
            {
                var v = (MyDoorDefinition)def;
                return MyTuple.Create(MyStringHash.GetOrCompute(v.ResourceSinkGroup), MyEnergyConstants.MAX_REQUIRED_POWER_DOOR);
            }
            if (def is MyMedicalRoomDefinition)
            {
                var v = (MyMedicalRoomDefinition)def;
                return MyTuple.Create(MyStringHash.GetOrCompute(v.ResourceSinkGroup), MyEnergyConstants.MAX_REQUIRED_POWER_MEDICAL_ROOM);
            }
            if (def is MySoundBlockDefinition)
            {
                var v = (MySoundBlockDefinition)def;
                return MyTuple.Create(v.ResourceSinkGroup, MyEnergyConstants.MAX_REQUIRED_POWER_SOUNDBLOCK);
            }
            return MyTuple.Create(MyStringHash.GetOrCompute("null"), 0.0f);
            // internal MyCryoChamberDefinition
            // ignore MyShipDrillDefinition
            // ignore MyShipGrinderDefinition
            // ignore MyShipWelderDefinition
        }

        private static readonly MyCache<MyCubeBlockDefinition, MyTuple<MyStringHash, float>> maxPowerCache = new MyCache<MyCubeBlockDefinition, MyTuple<MyStringHash, float>>(128);
        public static MyTuple<MyStringHash, float> MaxPowerConsumption(MyCubeBlockDefinition def)
        {
            return maxPowerCache.GetOrCreate(def, MaxPowerConsumptionInternal);
        }
    }
}
