using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils;
using Equinox.Utils.Collections;
using Equinox.Utils.Logging;
using Equinox.Utils.Random;
using VRage;
using VRage.Collections;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    [Flags]
    internal enum RoomCollisionFlag
    {
        Optional = 1,
        NoOptional = 2
    }

    internal static class RoomCollisionFlagExtensions
    {
        public static bool HasFlag(this RoomCollisionFlag entry, RoomCollisionFlag flag)
        {
            return (entry & flag) != 0;
        }
    }

    public class StationGenerator
    {
        #region RoomCompositeKey
        private class RoomKeyComparer : IEqualityComparer<RoomKey>
        {
            public static readonly RoomKeyComparer Instance = new RoomKeyComparer();

            public bool Equals(RoomKey x, RoomKey y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(RoomKey obj)
            {
                return obj.GetHashCode();
            }
        }

        private struct RoomKey
        {
            public readonly MatrixI Transformation;
            public readonly PartFromPrefab Part;

            public RoomKey(MatrixI transform, PartFromPrefab part)
            {
                Transformation = transform;
                Part = part;
            }

            public override int GetHashCode()
            {
                return Transformation.GetHashCode() * 71 ^ Part.GetHashCode();
            }

            public bool Equals(RoomKey other)
            {
                return MatrixIEqualityComparer.Instance.Equals(Transformation, other.Transformation) && other.Part == Part;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is RoomKey)) return false;
                return Equals((RoomKey)obj);
            }
        }
        #endregion

        #region RoomInfo
        private class RoomMeta
        {
            public readonly ProceduralRoom Room;
            public int InFactor;
            public int Nonce;
            public RoomCollisionFlag CollisionMask = 0;

            public RoomMeta(ProceduralRoom room)
            {
                Room = room;
                InFactor = 0;
            }

            public RoomKey Key => new RoomKey(Room.Transform, Room.Part);
        }
        #endregion

        private readonly Queue<ProceduralMountPoint> m_openMountPoints = new Queue<ProceduralMountPoint>();
        private readonly MultiDictionary<ProceduralMountPoint, RoomMeta> m_possibleRooms = new MultiDictionary<ProceduralMountPoint, RoomMeta>();
        private readonly Dictionary<RoomKey, RoomMeta> m_openRooms = new Dictionary<RoomKey, RoomMeta>(RoomKeyComparer.Instance);
        private readonly ProceduralConstruction m_construction;
        private readonly StationGeneratorManager m_manager;
        private int m_nonce;

        public Func<PartFromPrefab, bool> PartFilter = null;

        public StationGenerator(StationGeneratorManager manager, ProceduralConstruction construction)
        {
            m_manager = manager;
            m_construction = construction;
            foreach (var room in construction.Rooms)
                foreach (var mount in room.MountPoints)
                    if (mount.AttachedTo == null)
                        m_openMountPoints.Enqueue(mount);
        }

        /// <summary>
        /// Adds the room to the construction and clears the open list of invalid entries.
        /// </summary>
        /// <param name="room"></param>
        private void CommitRoom(ProceduralRoom room)
        {
            // Add to the construction
            m_construction.AddRoom(room);

            foreach (var mount in room.MountPoints)
            {
                var attach = mount.AttachedTo;
                if (attach == null)
                {
                    m_openMountPoints.Enqueue(mount);
                    continue;
                }
                // Clear the open list of invalid entries
                foreach (var other in m_possibleRooms[mount])
                {
                    other.InFactor--;
                    if (other.InFactor == 0)
                        m_openRooms.Remove(other.Key);
                }
                m_possibleRooms.Remove(mount);
            }
            m_openRooms.Remove(new RoomKey(room.Transform, room.Part));
        }

        /// <summary>
        /// Registers the given room as a possibility if it isn't already registered.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="part"></param>
        private RoomMeta RegisterKey(MatrixI transform, PartFromPrefab part)
        {
            var key = new RoomKey(transform, part);
            RoomMeta room;
            if (!m_openRooms.TryGetValue(key, out room))
            {
                var ent = new ProceduralRoom();
                ent.Init(transform, part);
                m_openRooms[key] = room = new RoomMeta(ent);
            }
            else if (room.Nonce == m_nonce)
                return room;
            room.Nonce = m_nonce;
            room.InFactor = 0;
            foreach (var mount in room.Room.MountPoints)
            {
                var other = mount.AttachedToIn(m_construction);
                if (other == null) continue;
                room.InFactor++;
                m_possibleRooms.Add(other, room);
            }
            return room;
        }

        private void ProcessOpenMountPoints()
        {
            ProceduralMountPoint mount;
            while (m_openMountPoints.TryDequeue(out mount))
            {
                foreach (var type in m_manager.PartManager)
                    if (PartFilter == null || PartFilter.Invoke(type))
                        foreach (var other in type.MountPointsOfType(mount.MountPoint.MountType))
                        {
                            var mats = mount.MountPoint.GetTransform(other);
                            if (mats == null) continue;
                            foreach (var mat in mats)
                            {
                                var actual = Utilities.Multiply(mat, mount.Owner.Transform);
                                var result = RegisterKey(actual, type);

                                if (result.InFactor == 0)
                                {
                                    m_manager.Warning(
                                        "In factor for room {0}, mount {1}:{2} is zero.  How did we get here?  Parent was {3}, mount {4}:{5}",
                                        type.Name, other.MountType, other.MountName,
                                        mount.Owner.Part.Name, mount.MountPoint.MountType, mount.MountPoint.MountName
                                    );
                                    using (m_manager.IndentUsing())
                                    {
                                        m_manager.Warning("My mount points");
                                        using (m_manager.IndentUsing())
                                            foreach (var x in result.Room.GetMountPoint(other).MountLocations)
                                                m_manager.Warning(" at {0}", x);
                                        m_manager.Warning("Opposing anchor points");
                                        using (m_manager.IndentUsing())
                                            foreach (var x in mount.AnchorLocations)
                                                m_manager.Warning(" at {0}", x);
                                    }
                                }
                            }
                        }
            }
        }

        // [ThreadStatic]
        private readonly HashSet<ProceduralMountPoint> m_invokers = new HashSet<ProceduralMountPoint>();
        private bool CollidesPredictive(ProceduralRoom room, bool testMounts, bool testOptional)
        {
            // Buildable?
            if (m_construction.Intersects(room, testOptional))
                return true;
            if (!testMounts)
                return false;

            m_invokers.Clear();
            // Reject if this will block another mount point, or one of our mount points would be blocked.
            // Quick test based on the mounting blocks themselves.
            foreach (var point in room.MountPoints)
            {
                var mount = point.AttachedToIn(m_construction);
                if (mount != null)
                    m_invokers.Add(mount);
                else if (point.MountLocations.Any(m_construction.CubeExists))
                        return true;
            }
            foreach (var other in m_construction.Rooms)
                if (other != room)
                    foreach (var point in other.MountPoints)
                        if (point.AttachedToIn(m_construction) == null)
                        {
                            foreach (var block in point.MountPoint.Blocks)
                            {
                                var pos = other.PrefabToGrid(block.MountLocation);
                                if (!room.CubeExists(pos)) continue;
                                var mountBlock = room.GetMountPointBlockAt(pos);
                                if (mountBlock == null || !mountBlock.TypeEquals(block) || pos != room.PrefabToGrid(mountBlock.AnchorLocation))
                                    return true;
                            }
                        }

            // Reject if this will block another mount point, or one of our mount points would blocked.  Use expensive test.
            foreach (var point in room.MountPoints)
                if (point.AttachedToIn(m_construction) == null)
                {
                    var oppos = point.MountPoint.SmallestTerminalAttachment;
                    if (oppos.Item1 == null) continue;
                    var pos = Utilities.Multiply(oppos.Item2, room.Transform);
                    MatrixI ipos;
                    MatrixI.Invert(ref pos, out ipos);
                    if (m_construction.Intersects(oppos.Item1, pos, ipos, testOptional, true, room))
                        return true;
                }
            // Compare to all other unused mount points.
            foreach (var other in m_construction.Rooms)
                if (other != room)
                    foreach (var point in other.MountPoints)
                        if (!m_invokers.Contains(point) && point.AttachedTo == null)
                        {
                            // TODO we actually have this data pre-computed if we wanted to use that.
                            var oppos = point.MountPoint.SmallestTerminalAttachment;
                            if (oppos.Item1 == null) continue;
                            var pos = Utilities.Multiply(oppos.Item2, other.Transform);
                            MatrixI ipos;
                            MatrixI.Invert(ref pos, out ipos);
                            if (PartMetadata.Intersects(room.Part, room.Transform, room.InvTransform,
                                other.Part, pos, ipos, testOptional, true))
                                return true;
                        }
            return false;
        }

        private readonly Dictionary<PartFromPrefab, double> m_errorByType = new Dictionary<PartFromPrefab, double>();
        private readonly WeightedChoice<ProceduralRoom> m_weightedChoice = new WeightedChoice<ProceduralRoom>();
        private void ProcessOpenRooms(bool testOptional, float targetGrowth)
        {
            var collisionMask = testOptional ? RoomCollisionFlag.Optional : RoomCollisionFlag.NoOptional;

            m_errorByType.Clear();
            m_weightedChoice.Clear();
            var entrySeedError = m_construction.ComputeErrorAgainstSeed();
            m_manager.Debug("Target growth {0}", targetGrowth);

            var boundingBox = BoundingBox.CreateInvalid();
            foreach (var k in m_construction.Rooms)
                boundingBox = boundingBox.Include(k.BoundingBoxBoth);

            foreach (var room in m_openRooms.Values)
            {
                if (room.CollisionMask.HasFlag(collisionMask)) continue;
                if (CollidesPredictive(room.Room, true, testOptional))
                {
                    room.CollisionMask |= collisionMask;
                    continue;
                }

                using (m_construction.RegisterRoomUsing(room.Room))
                {
                    var randomScore = 1e1 * m_construction.Seed.DeterministicNoise(room.Room.Part.Name.GetHashCode() ^ room.Room.Transform.GetHashCode());

                    double growthScore = 0;
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        var freeMountPointCount = m_possibleRooms.Backing.Keys.Count;
                        foreach (var point in room.Room.MountPoints)
                            if (point.AttachedTo != null)
                                count--;
                            else
                                count++;

                        // Are we trying to shrink but this room makes us grow?
                        if (targetGrowth < 0 && count > 0)
                        {
                            growthScore -= 1e20;
                        }
                        // Is the future mount count going to drop below zero while we are trying to grow?
                        if (freeMountPointCount + count <= 0 && targetGrowth >= 0)
                        {
                            growthScore -= 1e20;
                        }

                        var error = count - targetGrowth;
                        // We are reducing the number of mounts.  Divide by total mounts since this isn't an issue when we have lots of choices.
                        if (count <= 0)
                            growthScore -= error * error * 1e3 / Math.Sqrt(1 + freeMountPointCount);
                        else // increasing the number of mounts.  The more mounts we have the larger of an issue this is.
                            growthScore -= error * error * Math.Sqrt(1 + freeMountPointCount);
                    }

                    var sizeError = Vector3.DistanceSquared(boundingBox.Center, room.Room.BoundingBox.Center);
                    var boundingBoxNew = BoundingBox.CreateMerged(boundingBox, room.Room.BoundingBox);
                    sizeError += boundingBoxNew.Extents.Dot(boundingBoxNew.Extents);
                    sizeError *= 1e3f;

                    double roomError;
                    if (!m_errorByType.TryGetValue(room.Room.Part, out roomError))
                    {
                        var mySeedError = m_construction.ComputeErrorAgainstSeed();
                        roomError = m_errorByType[room.Room.Part] =
                            mySeedError - entrySeedError;
                        m_manager.Debug("    Type {0} has error {1:e}", room.Room.Part.Name, roomError);
                    }

                    double totalScore = 0;
                    totalScore += randomScore;
                    totalScore += growthScore;
                    totalScore -= sizeError;
                    totalScore -= roomError;

                    m_weightedChoice.Add(room.Room, (float)totalScore);
                }
            }
        }

        private void AppendRooms()
        {
            if (Settings.DebugGenerationStages)
                m_manager.Debug("Choose from {0} valid options", m_weightedChoice.Count);
            var bestRoom = m_weightedChoice.ChooseBest();

            // 50% chance to be in the top 1% of choices.
            var room = m_weightedChoice.ChooseByQuantile(m_construction.Seed.DeterministicNoise(m_construction.Rooms.Count()), 0.99);
            //            var room = bestRoom;
            var originalRequirementError = m_construction.ComputeErrorAgainstSeed();
            CommitRoom(room);
            var newError = m_construction.ComputeErrorAgainstSeed();

            if (Settings.DebugGenerationStages)
                m_manager.Debug("Added {0} (number {1}) at {2}. Sadness changed {3:e} => {4:e} = {5:e}.  Best was {6}",
                    room.Part.Name, m_construction.Rooms.Count(), room.BoundingBox.Center, originalRequirementError, newError, originalRequirementError - newError,
                    bestRoom?.Part.Name);
            else if (Settings.DebugGenerationResults)
                m_manager.Debug("Added {0} (number {1}) at {2}.", room.Part.Name, m_construction.Rooms.Count(), room.BoundingBox.Center);

            m_manager.Debug("    I'm at {0}.  Parents at {1}", room.BoundingBox.Center, string.Join(" ", room.MountPoints.Select(x => x.AttachedTo?.Owner?.BoundingBox.Center).Where(x => x != null).Select(x => x.Value)));
        }

        public bool StepGeneration(float targetGrowth = 0, bool testOptional = true)
        {
            m_nonce++;
            ProcessOpenMountPoints();
            ProcessOpenRooms(testOptional, targetGrowth);
            if (m_weightedChoice.Count == 0)
                return false;
            AppendRooms();
            return true;
        }
    }
}
