using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Generation;
using ProcBuild.Storage;
using ProcBuild.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ProcBuild.Creation
{
    public class MyConstructionCopy
    {
        public readonly MyObjectBuilder_CubeGrid m_primaryGrid;
        public readonly List<MyObjectBuilder_CubeGrid> m_auxGrids;

        public MyConstructionCopy(MyObjectBuilder_CubeGrid primaryGrid)
        {
            m_primaryGrid = primaryGrid;
            m_auxGrids = new List<MyObjectBuilder_CubeGrid>();
        }
    }

    public static class MyGridCreator
    {

        private static readonly MyRoomRemapper Remapper = new MyRoomRemapper();

        public static MyConstructionCopy SpawnRoomAt(MyProceduralRoom room, MatrixD spawnLocation)
        {
            var i = room.Part.PrimaryGrid;
            var o = new MyObjectBuilder_CubeGrid
            {
                GridSizeEnum = i.GridSizeEnum,
                IsStatic = true,
                DampenersEnabled = true,
                Handbrake = true,
                DisplayName = room.Owner.GetName(),
                DestructibleBlocks = true,
                IsRespawnGrid = false,
                Editable = true,
                PersistentFlags = MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene | MyPersistentEntityFlags2.CastShadows
            };
            // Center it
            spawnLocation.Translation -= Vector3D.TransformNormal(room.BoundingBox.Center, ref spawnLocation) * MyDefinitionManager.Static.GetCubeSize(o.GridSizeEnum);
            spawnLocation.Translation -= room.Part.Prefab.BoundingSphere.Center;
            o.PositionAndOrientation = new MyPositionAndOrientation(spawnLocation);

            var output = new MyConstructionCopy(o);
            Remapper.Remap(room, output);
            return output;
        }

        public static void AppendRoom(MyConstructionCopy dest, MyProceduralRoom room)
        {
            Remapper.Remap(room, dest);
        }
    }
}
