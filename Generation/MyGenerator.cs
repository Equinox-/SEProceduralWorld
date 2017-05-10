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
        private static bool ValidateAddition(MyProceduralConstruction c, MyProceduralRoom room, bool testOptional)
        {
            // Buildable?
            if (c.Intersects(room, testOptional))
                return false;

            // Reject if this will block another mount point, or one of our mount points would blocked.
            var selfRooms = new List<MyTuple<MyPart, MatrixI, MatrixI>>();
            foreach (var point in room.MountPoints)
                if (point.AttachedTo == null)
                {
                    var oppos = point.MountPoint.SmallestTerminalAttachment;
                    if (oppos.Item1 == null) continue;
                    var pos = MyUtilities.Multiply(oppos.Item2, room.Transform);
                    MatrixI ipos;
                    MatrixI.Invert(ref pos, out ipos);
                    if (c.Intersects(oppos.Item1, pos, ipos, testOptional))
                        return false;
                    selfRooms.Add(MyTuple.Create(oppos.Item1, pos, ipos));
                }
            
            // Compare to all other unused mount points.  Cache these?
            foreach (var other in c.Rooms)
                if (other != room)
                    foreach (var point in other.MountPoints)
                        if (point.AttachedTo == null)
                        {
                            var oppos = point.MountPoint.SmallestTerminalAttachment;
                            if (oppos.Item1 == null) continue;
                            var pos = MyUtilities.Multiply(oppos.Item2, other.Transform);
                            MatrixI ipos;
                            MatrixI.Invert(ref pos, out ipos);
                            if (!selfRooms.Any(self => MyPart.Intersects(self.Item1, self.Item2, self.Item3, other.Part, pos, ipos, testOptional))) continue;
                            return false;
                        }
            return true;
        }

        public static bool StepConstruction(MyProceduralConstruction c, float targetGrowth = 0, bool testOptional = true)
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
                            var actual = MyUtilities.Multiply(mat, point.Owner.Transform);
                            var key = MyTuple.Create(type, actual);
                            if (!availableRooms.ContainsKey(key))
                                c.RemoveRoom(availableRooms[key] = c.GenerateRoom(actual, type));
                        }
                    }
            SessionCore.Log("Choose from {0} options; generated in {1}", availableRooms.Count, iwatch.Elapsed);

            iwatch.Restart();
            // Compute room weights
            var weightedRoomChoice = new MyWeightedChoice<MyProceduralRoom>();
            foreach (var room in availableRooms.Values)
            {
                var portCover = room.Part.Name.Contains("PortCoverWindow");
                try
                {
                    room.TakeOwnership(c);
                    if (!ValidateAddition(c, room, testOptional))
                        continue;

                    var coolness = 0.0f;
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        foreach (var point in room.MountPoints)
                            if (point.AttachedTo != null)
                                count--;
                            else
                                count++;

                        if (targetGrowth < 0 && count > 0)
                            continue;
                        var error = count - targetGrowth;
                        coolness += 100 * (1 - error * error);
                    }
                    { // Coolness based on navmesh distance vs real distance

                    }
                    { // Less coolness for gravity generators with different orientations

                    }
                    { // Coolness based on how useful this room will be.
                      // If we need power and this room provides power this is high, etc...

                    }
                    { // Less coolness to rooms that hog resources either for operation or for building
                    }
                    coolness += (float)SessionCore.RANDOM.NextDouble();
                    weightedRoomChoice.Add(room, coolness);
                }
                finally
                {
                    c.RemoveRoom(room);
                }
            }
            SessionCore.Log("Choose from {0} valid options; generated weights in {1}", weightedRoomChoice.Count, iwatch.Elapsed);
            if (weightedRoomChoice.Count == 0)
                return false;
            {
                var room = weightedRoomChoice.Choose((float)SessionCore.RANDOM.NextDouble());
                room.TakeOwnership(c);
                SessionCore.Log("Added {0} (number {1}) at {2}", room.Part.Name, c.Rooms.Count(), room.BoundingBox.Center);
                return true;
            }
        }
    }
}
