using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils;
using VRage;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public partial class MyGenerator
    {
        private bool ValidateAddition(MyProceduralConstruction c, MyProceduralRoom room, bool testOptional)
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
                            if (!selfRooms.Any(self => MyPartStorage.Intersects(self.Item1, self.Item2, self.Item3, other.Part, pos, ipos, testOptional))) continue;
                            return false;
                        }
            return true;
        }

        private readonly HashSet<MyProceduralMountPoint> m_scannedMounts = new HashSet<MyProceduralMountPoint>();
        private readonly Dictionary<MyProceduralMountPoint, HashSet<MyProceduralRoom>> m_roomInvoker = new Dictionary<MyProceduralMountPoint, HashSet<MyProceduralRoom>>();
        private readonly Dictionary<MatrixI, Dictionary<MyPart, MyProceduralRoom>> m_openRoomsByPosition = new Dictionary<MatrixI, Dictionary<MyPart, MyProceduralRoom>>();

        private bool TryAddRoom(ref MatrixI transform, MyPart part)
        {
            Dictionary<MyPart, MyProceduralRoom> position;
            if (!m_openRoomsByPosition.TryGetValue(transform, out position))
                position = m_openRoomsByPosition[transform] = new Dictionary<MyPart, MyProceduralRoom>();
            else if (position.ContainsKey(part))
                return false;
            var room = m_construction.GenerateRoom(transform, part);
            foreach (var mount in room.MountPoints)
                if (mount.AttachedTo != null)
                {
                    HashSet<MyProceduralRoom> invoker;
                    if (!m_roomInvoker.TryGetValue(mount.AttachedTo, out invoker))
                        invoker = m_roomInvoker[mount.AttachedTo] = new HashSet<MyProceduralRoom>();
                    invoker.Add(room);
                }
            position[part] = room;
            m_construction.RemoveRoom(room);
            return true;
        }

        private void RoomWasAdded(MyProceduralRoom room)
        {
            foreach (var mount in room.MountPoints)
                if (mount.AttachedTo != null)
                {
                    // Register all our linked mounts.
                    m_scannedMounts.Add(mount);

                    // And de-register all that used our linked mount
                    foreach (var lk in m_roomInvoker[mount.AttachedTo])
                    {
                        if (lk == room) continue;
                        var seto = m_openRoomsByPosition[lk.Transform];
                        if (seto.Count == 0)
                            m_openRoomsByPosition.Remove(lk.Transform);
                    }
                    m_roomInvoker.Remove(mount.AttachedTo);
                }
            var set = m_openRoomsByPosition[room.Transform];
            set.Remove(room.Part);
            if (set.Count == 0)
                m_openRoomsByPosition.Remove(room.Transform);
        }

        private MyProceduralConstruction m_construction;
        public bool StepConstruction(MyProceduralConstruction c, float targetGrowth = 0, bool testOptional = true, Func<MyPart, bool> filter = null)
        {
            this.m_construction = c;
            var iwatch = new Stopwatch();
            iwatch.Restart();
            var freeMountPoints = c.Rooms.SelectMany(x => x.MountPoints).Where(x => x.AttachedTo == null).Where(x => !m_scannedMounts.Contains(x)).ToList();
            var selectFreeTime = iwatch.Elapsed;
            iwatch.Restart();
            var oldCount = m_openRoomsByPosition.Values.Sum(x => x.Count);
            // Compute Available Rooms
            var triedAdditions = 0;
            var triedPartMounts = 0;
            foreach (var type in SessionCore.Instance.PartManager)
                if (filter == null || filter.Invoke(type))
                    foreach (var point in freeMountPoints)
                        foreach (var other in type.MountPointsOfType(point.MountPoint.MountType))
                        {
                            var mats = point.MountPoint.GetTransform(other);
                            triedPartMounts++;
                            if (mats == null) continue;
                            foreach (var mat in mats)
                            {
                                var actual = MyUtilities.Multiply(mat, point.Owner.Transform);
                                TryAddRoom(ref actual, type);
                                triedAdditions++;
                            }
                        }
            foreach (var point in freeMountPoints)
                m_scannedMounts.Add(point);

            var availableRooms = m_openRoomsByPosition.Values.SelectMany(x => x.Values).ToList();
            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Choose from {0} options.  {1} additional from {2} free mounts; selected free in {3}, generated in {4}.  We looked at {5} position possibilities across {6} pairs", availableRooms.Count, availableRooms.Count - oldCount, freeMountPoints.Count, selectFreeTime, iwatch.Elapsed, triedAdditions, triedPartMounts);

            iwatch.Restart();
            // Compute room weights
            var weightedRoomChoice = new MyWeightedChoice<MyProceduralRoom>();

            var originalError = new List<string>();
            var originalRequirementError = c.ComputeErrorAgainstSeed(MyUtilities.LogToList(originalError));

            var collisionWatch = new Stopwatch();
            var inoutFactorWatch = new Stopwatch();
            var usefulnessWatch = new Stopwatch();

            var roomErrorCache = new Dictionary<MyPart, double>();

            var bestError = double.MaxValue;
            var bestErrMux = new List<string>();
            MyProceduralRoom bestRoom = null;
            foreach (var room in availableRooms)
            {
                try
                {
                    collisionWatch.Start();
                    room.TakeOwnership(c);
                    if (!ValidateAddition(c, room, testOptional))
                        continue;
                    collisionWatch.Stop();

                    var coolness = 0.0;
                    inoutFactorWatch.Start();
                    if (!float.IsNaN(targetGrowth))
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        foreach (var point in room.MountPoints)
                            if (point.AttachedTo != null)
                                count--;
                            else
                                count++;

                        if (targetGrowth < 0 && count > 0)
                            coolness -= 1e20; // super discouraged.  Last resort type thing.
                        var error = count - targetGrowth;
                        // We are reducing the number of mounts.  Divide by total mounts since this isn't an issue when we have lots of choices.
                        if (error <= 0)
                            coolness -= 1e10 * error * error / m_roomInvoker.Count;
                        else // increasing the number of mounts.  The more mounts we have the larger of an issue this is.
                            coolness -= 100 * error * error * Math.Sqrt(1 + m_roomInvoker.Count);
                    }
                    inoutFactorWatch.Stop();

                    { // Coolness based on navmesh distance vs real distance.
                      // If we do this we need to include the future rooms.
                    }
                    { // Less coolness for gravity generators with different orientations

                    }

                    usefulnessWatch.Start();
                    { // Coolness based on how useful this room will be.
                      // If we need power and this room provides power this is high, etc...

                        // This is the same per added room type.
                        double newRequirementError;
                        if (!roomErrorCache.TryGetValue(room.Part, out newRequirementError))
                            roomErrorCache[room.Part] = newRequirementError = c.ComputeErrorAgainstSeed();
                        if (bestError > newRequirementError)
                        {
                            bestErrMux.Clear();
                            c.ComputeErrorAgainstSeed(MyUtilities.LogToList(bestErrMux));
                            bestError = newRequirementError;
                            bestRoom = room;
                        }
                        var avgError = Math.Max(1, (originalRequirementError + newRequirementError) / 2);
                        var error = (originalRequirementError - newRequirementError);
                        const double errorMultiplier = 1e-4;
                        // When error is >0 we've improved the system, so encourage it.
                        coolness += errorMultiplier * error;
                    }
                    usefulnessWatch.Stop();

                    { // Less coolness to rooms that hog resources either for operation or for building
                    }
                    coolness += c.Seed.DeterministicNoise(room.Part.Name.GetHashCode() ^ room.Transform.GetHashCode());
                    weightedRoomChoice.Add(room, (float)coolness);
                }
                finally
                {
                    c.RemoveRoom(room);
                }
            }
            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Choose from {0} valid options; generated weights in {1}.  Collision in {2}, inout in {3}, usefulness in {4}",
                weightedRoomChoice.Count, iwatch.Elapsed, collisionWatch.Elapsed, inoutFactorWatch.Elapsed, usefulnessWatch.Elapsed);

            if (weightedRoomChoice.Count == 0)
                return false;
            {
                // 50% chance to be in the top 1% of choices.
                var room = weightedRoomChoice.ChooseByQuantile(c.Seed.DeterministicNoise(c.Rooms.Count()), 0.99);
                room.TakeOwnership(c);
                RoomWasAdded(room);

                var newErrMux = new List<string>();
                var newError = c.ComputeErrorAgainstSeed(MyUtilities.LogToList(newErrMux));

                if (Settings.Instance.DebugGenerationStagesWeights)
                    for (var i = 0; i < Math.Min(newErrMux.Count, Math.Min(bestErrMux.Count, originalError.Count)); i++)
                    {
                        if (originalError[i].Equals(newErrMux[i]) && bestErrMux[i].Equals(newErrMux[i])) continue;
                        SessionCore.Log("Old {0}", originalError[i]);
                        SessionCore.Log("New {0}", newErrMux[i]);
                        SessionCore.Log("Best {0}", bestErrMux[i]);
                    }
                if (Settings.Instance.DebugGenerationStages)
                    SessionCore.Log("Added {0} (number {1}) at {2}. Sadness changed {3:e} => {4:e} = {5:e}. Best was {6} with {7:e} less",
                        room.Part.Name, c.Rooms.Count(), room.BoundingBox.Center, originalRequirementError, newError, originalRequirementError - newError,
                        bestRoom?.Part.Name, newError - bestError);
                return true;
            }
        }
    }
}
