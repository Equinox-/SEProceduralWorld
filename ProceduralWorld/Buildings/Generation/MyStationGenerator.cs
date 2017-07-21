using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    [Flags]
    internal enum MyRoomCollisionFlag
    {
        Optional = 1,
        NoOptional = 2
    }

    internal static class MyRoomCollisionFlagExtensions
    {
        public static bool HasFlag(this MyRoomCollisionFlag entry, MyRoomCollisionFlag flag)
        {
            return (entry & flag) != 0;
        }
    }

    public class MyStationGenerator
    {
        #region RoomCompositeKey
        private class MyRoomKeyComparer : IEqualityComparer<MyRoomKey>
        {
            public static readonly MyRoomKeyComparer Instance = new MyRoomKeyComparer();

            public bool Equals(MyRoomKey x, MyRoomKey y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(MyRoomKey obj)
            {
                return obj.GetHashCode();
            }
        }

        private struct MyRoomKey
        {
            public readonly MatrixI Transformation;
            public readonly MyPartFromPrefab Part;

            public MyRoomKey(MatrixI transform, MyPartFromPrefab part)
            {
                Transformation = transform;
                Part = part;
            }

            public override int GetHashCode()
            {
                return Transformation.GetHashCode() * 71 ^ Part.GetHashCode();
            }

            public bool Equals(MyRoomKey other)
            {
                return MyMatrixIEqualityComparer.Instance.Equals(Transformation, other.Transformation) && other.Part == Part;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is MyRoomKey)) return false;
                return Equals((MyRoomKey)obj);
            }
        }
        #endregion

        #region RoomInfo

        private class MyRoomMeta
        {
            public readonly MyProceduralRoom Room;
            public int InFactor;
            public int Nonce;
            public MyRoomCollisionFlag CollisionMask = 0;

            public MyRoomMeta(MyProceduralRoom room)
            {
                Room = room;
                InFactor = 0;
            }

            public MyRoomKey Key => new MyRoomKey(Room.Transform, Room.Part);
        }
        #endregion

        private readonly Queue<MyProceduralMountPoint> m_openMountPoints = new Queue<MyProceduralMountPoint>();
        private readonly MyMultiDictionary<MyProceduralMountPoint, MyRoomMeta> m_possibleRooms = new MyMultiDictionary<MyProceduralMountPoint, MyRoomMeta>();
        private readonly Dictionary<MyRoomKey, MyRoomMeta> m_openRooms = new Dictionary<MyRoomKey, MyRoomMeta>(MyRoomKeyComparer.Instance);
        private readonly MyProceduralConstruction m_construction;
        private int m_nonce;

        public Func<MyPartFromPrefab, bool> PartFilter = null;

        public MyStationGenerator(MyProceduralConstruction construction)
        {
            foreach (var room in construction.Rooms)
                foreach (var mount in room.MountPoints)
                    if (mount.AttachedTo == null)
                        m_openMountPoints.Enqueue(mount);
        }

        /// <summary>
        /// Adds the room to the construction and clears the open list of invalid entries.
        /// </summary>
        /// <param name="room"></param>
        private void CommitRoom(MyProceduralRoom room)
        {
            // Add to the construction
            m_construction.RegisterRoom(room);

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
        }

        /// <summary>
        /// Registers the given room as a possibility if it isn't already registered.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="part"></param>
        private void RegisterKey(MatrixI transform, MyPartFromPrefab part)
        {
            var key = new MyRoomKey(transform, part);
            MyRoomMeta room;
            if (!m_openRooms.TryGetValue(key, out room))
            {
                var ent = new MyProceduralRoom();
                ent.Init(transform, part);
                m_openRooms[key] = room = new MyRoomMeta(ent);
            }
            else if (room.Nonce == m_nonce)
                return;
            room.Nonce = m_nonce;
            room.InFactor = 0;
            foreach (var mount in room.Room.MountPoints)
            {
                var other = mount.AttachedToIn(m_construction);
                if (other == null) continue;
                room.InFactor++;
                m_possibleRooms.Add(other, room);
            }
        }

        private void ProcessOpenMountPoints()
        {
            MyProceduralMountPoint mount;
            while (m_openMountPoints.TryDequeue(out mount))
            {
                foreach (var type in SessionCore.Instance.PartManager)
                    if (PartFilter == null || PartFilter.Invoke(type))
                        foreach (var other in type.MountPointsOfType(mount.MountPoint.MountType))
                        {
                            var mats = mount.MountPoint.GetTransform(other);
                            if (mats == null) continue;
                            foreach (var mat in mats)
                            {
                                var actual = MyUtilities.Multiply(mat, mount.Owner.Transform);
                                RegisterKey(actual, type);
                            }
                        }
            }
        }

        private readonly Dictionary<MyPartFromPrefab, double> m_errorByType = new Dictionary<MyPartFromPrefab, double>();
        private readonly MyWeightedChoice<MyProceduralRoom> m_weightedChoice = new MyWeightedChoice<MyProceduralRoom>();
        private void ProcessOpenRooms(bool testOptional, float targetGrowth)
        {
            var collisionMask = testOptional ? MyRoomCollisionFlag.Optional : MyRoomCollisionFlag.NoOptional;

            m_errorByType.Clear();
            m_weightedChoice.Clear();
            var entryScore = m_construction.ComputeErrorAgainstSeed();
            foreach (var room in m_openRooms.Values)
            {
                if (room.CollisionMask.HasFlag(collisionMask)) continue;
                using (m_construction.RegisterRoomUsing(room.Room))
                {
                    if (m_construction.Intersects(room.Room, testOptional))
                    {
                        room.CollisionMask |= collisionMask;
                        continue;
                    }

                    var roomScore = m_construction.Seed.DeterministicNoise(room.Room.Part.Name.GetHashCode() ^ room.Room.Transform.GetHashCode());

                    double growthScore = 0;
                    { // Based on the target growth and destruct rates
                        var count = 0;
                        var freeMountPointCount = m_possibleRooms.Backing.Keys.Count;
                        foreach (var point in room.Room.MountPoints)
                            if (point.AttachedTo != null)
                                count--;
                            else
                                count++;

                        if (targetGrowth < 0 && count > 0)
                            growthScore -= 1e20; // super discouraged.  Last resort type thing.
                        var error = count - targetGrowth;
                        // Is the future mount count going to drop below zero while we are trying to grow?
                        if (freeMountPointCount + count <= 0 && targetGrowth >= 0)
                            growthScore -= 1e20;

                        // We are reducing the number of mounts.  Divide by total mounts since this isn't an issue when we have lots of choices.
                        if (error <= 0)
                            growthScore -= 100 * error * error / Math.Sqrt(1 + freeMountPointCount);
                        else // increasing the number of mounts.  The more mounts we have the larger of an issue this is.
                            growthScore -= 100 * error * error * Math.Sqrt(1 + freeMountPointCount);
                    }

                    double roomError;
                    if (!m_errorByType.TryGetValue(room.Room.Part, out roomError))
                        roomError = m_errorByType[room.Room.Part] = m_construction.ComputeErrorAgainstSeed() - entryScore;

                    growthScore -= 1e-4 * roomError;

                    m_weightedChoice.Add(room.Room, (float)growthScore);
                }
            }
        }

        private void AppendRooms()
        {
            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Choose from {0} valid options", m_weightedChoice.Count);
            var bestRoom = m_weightedChoice.ChooseBest();

            // 50% chance to be in the top 1% of choices.
            // var room = m_weightedRoomChoice.ChooseByQuantile(c.Seed.DeterministicNoise(c.Rooms.Count()), 0.99);
            var room = bestRoom;
            var originalRequirementError = m_construction.ComputeErrorAgainstSeed();
            CommitRoom(room);
            var newError = m_construction.ComputeErrorAgainstSeed();

            if (Settings.Instance.DebugGenerationStages)
                SessionCore.Log("Added {0} (number {1}) at {2}. Sadness changed {3:e} => {4:e} = {5:e}.  Best was {6}",
                    room.Part.Name, m_construction.Rooms.Count(), room.BoundingBox.Center, originalRequirementError, newError, originalRequirementError - newError,
                    bestRoom?.Part.Name);
            else if (Settings.Instance.DebugGenerationResults)
                SessionCore.Log("Added {0} (number {1}) at {2}.", room.Part.Name, m_construction.Rooms.Count(), room.BoundingBox.Center);
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
