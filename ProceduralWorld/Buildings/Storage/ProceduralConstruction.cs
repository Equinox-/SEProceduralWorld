using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Storage
{
    public class ProceduralConstruction
    {
        private readonly MyDynamicAABBTree m_roomTree;
        private readonly Dictionary<Vector3I, ProceduralMountPoint> m_mountPoints;
        private readonly Dictionary<int, ProceduralRoom> m_rooms;
        private readonly List<ProceduralRoom> m_roomsSafeOrder;

        public readonly ProceduralConstructionSeed Seed;
        public readonly BlockSetInfo BlockSetInfo;

        public readonly ILogging Logger;

        public ProceduralConstruction(ILogging logBase, ProceduralConstructionSeed seed)
        {
            Logger = logBase.Root().CreateProxy(GetType().Name);
            m_roomTree = new MyDynamicAABBTree(Vector3.Zero);
            m_rooms = new Dictionary<int, ProceduralRoom>();
            m_mountPoints = new Dictionary<Vector3I, ProceduralMountPoint>();
            m_roomsSafeOrder = new List<ProceduralRoom>();
            Seed = seed;
            BlockSetInfo = new BlockSetInfo();
        }

        public event Action<ProceduralRoom> RoomAdded;
        public event Action<ProceduralRoom> RoomRemoved;

        public double ComputeErrorAgainstSeed(Utilities.LoggingCallback logger = null)
        {
            var error = 0.0;
            {
                var err = BlockSetInfo.TotalRawResources;
                logger?.Invoke("Raw resources {0:e}.  Err {1:e}", BlockSetInfo.TotalRawResources, err);
                error += err;
            }
            // Block counts
            {
                var countCopy = new Dictionary<SupportedBlockTypes, int>(SupportedBlockTypesEquality.Instance);
                foreach (var kv in Seed.BlockCountRequirements)
                    countCopy[kv.Key] = 0;
                foreach (var kv in BlockSetInfo.BlockCountByType)
                {
                    var def = MyDefinitionManager.Static.GetCubeBlockDefinition(kv.Key);
                    foreach (var kt in Seed.BlockCountRequirements)
                        if (kt.Key.IsOfType(def))
                            countCopy.AddValue(kt.Key, kv.Value);
                }
                foreach (var kv in countCopy)
                {
                    var target = Seed.BlockCountRequirement(kv.Key);
                    var err = ErrorFunction(target.Count, kv.Value, 1e5, 0.1) * target.Multiplier;
                    logger?.Invoke("Block {0} count current {1:e} vs {2:e}. Err {3:e}", kv.Key, kv.Value, target.Count, err);
                    error += err;
                }
            }

            var powerReq = new ProceduralConstructionSeed.TradeRequirements(0, 0);
            foreach (var kv in Seed.ProductionStorage)
            {
                var id = kv.Key;
                var req = kv.Value;
                if (id == MyResourceDistributorComponent.ElectricityId)
                {
                    powerReq = req;
                    continue;
                }
                if (id.TypeId == typeof(MyObjectBuilder_GasProperties))
                {
                    var storageCurrent = BlockSetInfo.TotalGasStorage(id);
                    var ce = ErrorFunction(req.Storage, storageCurrent, 10, 1e-5) * req.StorageErrorMultiplier;
                    logger?.Invoke("Gas {0} storage current {1:e} vs {2:e}. Err {3:e}", id.SubtypeName, storageCurrent, req.Storage, ce);
                    error += ce;
                }
                var throughputCurrent = BlockSetInfo.TotalProduction(id);
                var te = ErrorFunction(req.Throughput, throughputCurrent, 1, 1e-6) * req.ThroughputErrorMultiplier;
                logger?.Invoke("{0} throughput current {1:e} vs {2:e}.  Store {3:e}. Err {4:e}", id, throughputCurrent, req.Throughput, req.Storage, te);
                error += te;
            }
            error /= Seed.LocalStorage.Count();

            // inventory:
            var errInventory = ErrorFunction(Seed.StorageVolume, BlockSetInfo.TotalInventoryCapacity, 1e3, 1e-3);
            logger?.Invoke("Inventory capacity current {0:e} vs {1:e}.  Err {2:e}", BlockSetInfo.TotalInventoryCapacity, Seed.StorageVolume, errInventory);
            error += errInventory;

            // power:
            // throughput is extra power available.  Total power consumption is (consumption - production).
            var errPowerThroughput = ErrorFunction(powerReq.Throughput, -BlockSetInfo.TotalPowerNetConsumption, 1e9, 0) * powerReq.ThroughputErrorMultiplier;
            var errPowerStorage = ErrorFunction(powerReq.Storage, BlockSetInfo.TotalPowerStorage, 1e7, 0) * powerReq.StorageErrorMultiplier;
            logger?.Invoke("Power throughput current {0:e} vs {1:e}. Err {2:e}", -BlockSetInfo.TotalPowerNetConsumption, powerReq.Throughput, errPowerThroughput);
            logger?.Invoke("Power capacity current {0:e} vs {1:e}. Err {2:e}", BlockSetInfo.TotalPowerStorage, powerReq.Storage, errPowerStorage);
            error += errPowerThroughput;
            error += errPowerStorage;
            return error;
        }

        private static double ErrorFunction(double target, double current, double multDeficit, double multSurplus)
        {
            var error = target - current;
            if (error > 0)
                return multDeficit * error * error;
            error += target;
            if (error > 0)
                return 0;
            else
                return multSurplus * error * error;
        }


        public void Clear()
        {
            foreach (var copy in m_rooms.ToList())
                RemoveRoom(copy.Value);
        }

        public bool Init(PartManager manager, Ob_ProceduralConstruction ob)
        {
            if (ob.Rooms.Any(x => manager.LoadNullable(x.PrefabID) == null))
                return false;
            Clear();
            foreach (var room in ob.Rooms)
            {
                var data = new ProceduralRoom();
                data.Init(room.Transform, manager.LoadNullable(room.PrefabID));
                AddRoom(data);
            }
            return true;
        }

        public Ob_ProceduralConstruction GetObjectBuilder()
        {
            return new Ob_ProceduralConstruction()
            {
                Rooms = m_rooms.Values.Select(x => x.GetObjectBuilder()).ToArray()
            };
        }

        private struct RoomRegisterToken : IDisposable
        {
            private readonly ProceduralConstruction m_construction;
            private readonly ProceduralRoom m_room;

            public RoomRegisterToken(ProceduralConstruction c, ProceduralRoom r)
            {
                m_construction = c;
                m_room = r;
                c.AddRoom(r);
            }

            public void Dispose()
            {
                m_construction.RemoveRoom(m_room);
            }
        }

        public IDisposable RegisterRoomUsing(ProceduralRoom room)
        {
            return new RoomRegisterToken(this, room);
        }

        public void AddRoom(ProceduralRoom room)
        {
            if (m_rooms.ContainsKey(room.RoomID))
                throw new ArgumentException("Room ID already used");
            m_rooms[room.RoomID] = room;
            var aabb = room.BoundingBoxBoth;
            room.m_aabbProxyID = m_roomTree.AddProxy(ref aabb, room, 0);
            m_roomsSafeOrder.Add(room);
            foreach (var k in room.MountPoints)
                foreach (var p in k.MountLocations)
                    if (m_mountPoints.ContainsKey(p))
                        Logger.Warning("Room {0} at {1} has mount point {4}:{5} that intersect with mount point {6}:{7} of room {2} at {3}", m_mountPoints[p].Owner.Part.Name, m_mountPoints[p].Owner.Transform.Translation,
                            room.Part.Name, room.Transform.Translation, m_mountPoints[p].MountPoint.MountType, m_mountPoints[p].MountPoint.MountName,
                            k.MountPoint.MountType, k.MountPoint.MountName);
                    else
                        m_mountPoints.Add(p, k);
            room.TakeOwnership(this);
            using (room.Part.LockSharedUsing())
                BlockSetInfo.AddToSelf(room.Part.BlockSetInfo);
            RoomAdded?.Invoke(room);
        }

        public void RemoveRoom(ProceduralRoom room)
        {
            m_rooms.Remove(room.RoomID);
            m_roomTree.RemoveProxy(room.m_aabbProxyID);
            if (m_roomsSafeOrder[m_roomsSafeOrder.Count - 1] == room)
                m_roomsSafeOrder.RemoveAt(m_roomsSafeOrder.Count - 1);
            else
            {
                m_roomsSafeOrder.Remove(room);
                Logger.Warning("Possibly unsafe removal of room not at end of safe list");
            }
            foreach (var k in room.MountPoints)
                foreach (var p in k.MountLocations)
                    if (!m_mountPoints.Remove(p))
                        Logger.Warning("Failed to remove room; mount point wasn't registered");
            room.Orphan();
            using (room.Part.LockSharedUsing())
                BlockSetInfo.SubtractFromSelf(room.Part.BlockSetInfo);
            RoomRemoved?.Invoke(room);
        }

        public ProceduralMountPoint MountPointAt(Vector3I v)
        {
            return m_mountPoints.GetValueOrDefault(v);
        }

        public ProceduralRoom GetRoomAt(Vector3I pos)
        {
            ProceduralRoom result = null;
            var aabb = new BoundingBox(pos, pos + 1);
            m_roomTree.Query((id) =>
            {
                var room = m_roomTree.GetUserData<ProceduralRoom>(id);
                if (!room.CubeExists(pos)) return true;
                result = room;
                return false;
            }, ref aabb);
            return result;
        }

        public bool CubeExists(Vector3I pos)
        {
            return GetRoomAt(pos) != null;
        }

        public MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            return GetRoomAt(pos)?.GetCubeAt(pos);
        }

        public bool Intersects(PartFromPrefab other, MatrixI otherTransform, MatrixI otherITransform, bool testOptional, bool testQuick = false, ProceduralRoom ignore = null)
        {
            var bb = Utilities.TransformBoundingBox(other.BoundingBoxBoth, otherTransform);
            var result = false;
            m_roomTree.Query((x) =>
            {
                var test = m_roomTree.GetUserData<ProceduralRoom>(x);
                var res = test != ignore && test.Intersects(other, otherTransform, otherITransform, testOptional, testQuick);
                result = res;
                return !res;
            }, ref bb);
            return result;
        }

        public bool Intersects(ProceduralRoom room, bool testOptional, bool testQuick = false)
        {
            var bb = room.BoundingBoxBoth;
            var result = false;
            m_roomTree.Query((x) =>
            {
                var test = m_roomTree.GetUserData<ProceduralRoom>(x);
                var res = test != room && test.Intersects(room, testOptional, testQuick);
                result = res;
                return !res;
            }, ref bb);
            return result;
        }

        public IEnumerable<ProceduralRoom> Rooms => m_roomsSafeOrder;

        public ProceduralRoom GetRoom(int key)
        {
            return m_rooms.GetValueOrDefault(key, null);
        }
    }
}
