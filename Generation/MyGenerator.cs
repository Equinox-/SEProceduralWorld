using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuffPanel.Logging;
using ProcBuild.Construction;
using VRage;
using VRageMath;
using ProcBuild.Utils;

namespace ProcBuild.Generation
{
    public class MyGenerator
    {
        public static bool StepConstruction(MyProceduralConstruction c, float targetGrowth = 0)
        {
            var freeMountPoints = c.Rooms.SelectMany(x => x.MountPoints).Where(x => x.AttachedTo == null).ToList();

            var iwatch = new Stopwatch();
            iwatch.Restart();
            // Compute Available Rooms
            var availableRooms = new Dictionary<MyTuple<MyPart, MatrixI>, MyProceduralRoom>();
            foreach (var type in SessionCore.Instance.PartManager)
                foreach (var point in freeMountPoints)
                    foreach (var other in type.MountPointsOfType(point.MountPoint.m_mountType))
                    {
                        var mats = point.MountPoint.GetTransform(other);
                        if (mats == null) continue;
                        foreach (var mat in mats)
                        {
                            var key = MyTuple.Create(type, mat);
                            if (availableRooms.ContainsKey(key))
                            {
                                c.RemoveRoom(availableRooms[key] = c.GenerateRoom(mat, type));
                            }
                        }
                    }
            SessionCore.Log("Choose from {0} options; generated in {1}", availableRooms.Count, iwatch.Elapsed);

            // Compute room weights
            var weightedRoomChoice = new MyWeightedChoice<MyProceduralRoom>();
            foreach (var room in availableRooms.Values)
            {
                try
                {
                    room.TakeOwnership(c);
                    // Buildable?
                    if (c.Intersects(room)) continue;

                    var coolness = 0.0f;
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        foreach (var point in room.MountPoints)
                            if (point.AttachedTo != null)
                                count -= 2; // point and opposition
                            else
                                count++;

                        coolness -= Math.Abs(count - targetGrowth);
                    }
                    weightedRoomChoice.Add(room, coolness);
                }
                finally
                {
                    c.RemoveRoom(room);
                }
            }
            if (weightedRoomChoice.Count == 0)
                return false;
            {
                var room = weightedRoomChoice.Choose((float) SessionCore.RANDOM.NextDouble());
                room.TakeOwnership(c);
                SessionCore.Log("Added {0} (number {1}) at {2}", room.Prefab.m_prefab.Id.SubtypeName, c.Rooms.Count(), room.Transform.Translation);
                return true;
            }
        }
    }
}
