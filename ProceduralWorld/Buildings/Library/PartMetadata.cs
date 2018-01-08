using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Definitions;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public partial class PartMetadata
    {
        public const string MountPrefix = "Dummy";
        public const string ReservedSpacePrefix = "ReservedSpace";
        public const string MultiUseSentinel = "&&";

        private readonly Dictionary<string, Dictionary<string, PartMount>> m_mountPoints;
        private readonly Dictionary<Vector3I, MyObjectBuilder_CubeBlock> m_blocks;
        private readonly Dictionary<Vector3I, PartMountPointBlock> m_mountPointBlocks;
        public BlockSetInfo BlockSetInfo { get; private set; }
        private readonly List<ReservedSpace> m_reservedSpaces;

        /// <summary>
        /// Bounding box enclosing the primary grid
        /// </summary>
        public BoundingBox BoundingBox { get; private set; }

        /// <summary>
        /// Bounding box enclosing all reserved spaces
        /// </summary>
        public BoundingBox ReservedSpace { get; private set; }

        /// <summary>
        /// Part manager responsible for this part
        /// </summary>
        public PartManager Manager { get; }

        protected ILogging Logger => Manager;

        public PartMetadata(PartManager manager)
        {
            Manager = manager;
            BlockSetInfo = new BlockSetInfo();
            m_mountPoints = new Dictionary<string, Dictionary<string, PartMount>>();
            m_mountPointBlocks = new Dictionary<Vector3I, PartMountPointBlock>(128, Vector3I.Comparer);
            m_reservedSpaces = new List<ReservedSpace>();
            m_blocks = new Dictionary<Vector3I, MyObjectBuilder_CubeBlock>(256, Vector3I.Comparer);
        }


        public virtual void Init(Ob_Part v)
        {
            BlockSetInfo.BlockCountByType.Clear();
            foreach (var kv in v.BlockCountByType)
                BlockSetInfo.BlockCountByType[kv.Item1] = kv.Item2;

            BlockSetInfo.ComponentCost.Clear();
            foreach (var kv in v.ComponentCost)
                BlockSetInfo.ComponentCost[MyDefinitionManager.Static.GetComponentDefinition(kv.Item1)] = kv.Item2;

            m_blocks.Clear();
            foreach (var kv in v.OccupiedLocations)
                m_blocks[kv] = null;

            BlockSetInfo.PowerConsumptionByGroup.Clear();
            foreach (var kv in v.PowerConsumptionByGroup)
                BlockSetInfo.PowerConsumptionByGroup[kv.Item1] = kv.Item2;

            m_reservedSpaces.Clear();
            m_reservedSpaces.AddRange(v.ReservedSpaces.Select(x => new ReservedSpace(x)));

            m_mountPoints.Clear();
            m_mountPointBlocks.Clear();
            foreach (var mp in v.MountPoints)
            {
                var block = new PartMount(this, mp.Type, mp.Name);
                block.Init(mp);

                Dictionary<string, PartMount> partsOfType;
                if (!m_mountPoints.TryGetValue(mp.Type, out partsOfType))
                    partsOfType = m_mountPoints[mp.Type] = new Dictionary<string, PartMount>();

                partsOfType[mp.Name] = block;
                foreach (var kv in block.Blocks)
                    m_mountPointBlocks[kv.AnchorLocation] = kv;
            }

            // Load AABBs
            BoundingBox = BoundingBox.CreateInvalid();
            foreach (var p in v.OccupiedLocations)
                BoundingBox = BoundingBox.Include((Vector3I)p);
            ReservedSpace = BoundingBox.CreateInvalid();
            foreach (var r in v.ReservedSpaces)
            {
                ReservedSpace = ReservedSpace.Include(r.Min);
                ReservedSpace = ReservedSpace.Include(r.Max);
            }

            BlockSetInfo.UpdateCache();

            Logger.Info("Loaded {0} lazily with {1} mount points, {2} reserved spaces, and {3} occupied cubes.", Name, MountPoints.Count(), m_reservedSpaces.Count, m_blocks.Count);
            foreach (var type in MountPointTypes)
                Logger.Info("    ...of type \"{0}\" there are {1}", type, MountPointsOfType(type).Count());
        }

        public Ob_Part GetObjectBuilder()
        {
            using (LockSharedUsing())
            {
                var res = new Ob_Part
                {
                    BuilderVersion = Ob_Part.PartBuilderVersion,
                    BlockCountByType = BlockSetInfo.BlockCountByType.Select(x => SerializableTuple.Create((SerializableDefinitionId)x.Key, x.Value)).ToArray(),
                    ComponentCost = BlockSetInfo.ComponentCost.Select(x => SerializableTuple.Create((SerializableDefinitionId)x.Key.Id, x.Value)).ToArray(),
                    OccupiedLocations = m_blocks.Keys.Select(x => (SerializableVector3I)x).ToArray(),
                    PowerConsumptionByGroup = BlockSetInfo.PowerConsumptionByGroup.Select(x => SerializableTuple.Create(x.Key, x.Value)).ToArray(),
                    ReservedSpaces = m_reservedSpaces.Select(x => x.GetObjectBuilder()).ToArray(),
                    MountPoints = m_mountPoints.Values.SelectMany(x => x.Values).Select(x => x.GetObjectBuilder()).ToArray()
                };
                return res;
            }
        }


        #region Mount Points
        public IEnumerable<string> MountPointTypes => m_mountPoints.Keys;

        public IEnumerable<PartMount> MountPoints => (m_mountPoints.Values.SelectMany(x => x.Values));

        public IEnumerable<PartMount> MountPointsOfType(string type)
        {
            using (LockSharedUsing())
            {
                Dictionary<string, PartMount> typed;
                return m_mountPoints.TryGetValue(type, out typed) ? typed.Values : Enumerable.Empty<PartMount>();
            }
        }

        public PartMountPointBlock MountPointAt(Vector3I pos)
        {
            using (LockSharedUsing())
            {
                PartMountPointBlock mount;
                return m_mountPointBlocks.TryGetValue(pos, out mount) ? mount : null;
            }
        }

        public PartMount MountPoint(string typeID, string instanceID)
        {
            using (LockSharedUsing())
            {
                Dictionary<string, PartMount> instan;
                if (!m_mountPoints.TryGetValue(typeID, out instan))
                    return null;
                PartMount part = null;
                if (instan?.TryGetValue(instanceID, out part) ?? false)
                    return part;
                return null;
            }
        }
        #endregion

        /// <summary>
        /// Bounding box including both the grid and all reserved spaces
        /// </summary>
        public BoundingBox BoundingBoxBoth
        {
            get
            {
                using (LockSharedUsing())
                {
                    var box = new BoundingBox(BoundingBox.Min, BoundingBox.Max);
                    if (m_reservedSpaces.Count > 0)
                        box = box.Include(ReservedSpace);
                    return box;
                }
            }
        }

        public IEnumerable<ReservedSpace> ReservedSpaces => m_reservedSpaces;

        #region MetaCompute

        private bool m_initFromGrid = false;
        private readonly FastResourceLock m_lock = new FastResourceLock();

        public FastResourceLockExtensions.MySharedLock LockSharedUsing()
        {
            return m_lock.AcquireSharedUsing();
        }

        public void InitFromGrids(MyObjectBuilder_CubeGrid primaryGrid, ICollection<MyObjectBuilder_CubeGrid> allGrids)
        {
            try
            {
                if (!m_lock.TryAcquireExclusive())
                {
                    m_lock.AcquireExclusive();
                    if (m_initFromGrid)
                        return;
                }
                // References to BlockInfo aren't threadsafe, so create a new one for this purpose.
                var blockInfo = new BlockSetInfo();
                ComputeBlockMap(primaryGrid, allGrids, blockInfo);
                ComputeReservedSpace(primaryGrid, allGrids, blockInfo);
                ComputeMountPoints(primaryGrid, allGrids, blockInfo);
                blockInfo.UpdateCache();
                BlockSetInfo = blockInfo;

                m_initFromGrid = true;
            }
            finally
            {
                m_lock.ReleaseExclusive();
            }
        }

        private void ComputeReservedSpace(MyObjectBuilder_CubeGrid primaryGrid, IEnumerable<MyObjectBuilder_CubeGrid> allGrids, BlockSetInfo info)
        {
            m_reservedSpaces.Clear();
            ReservedSpace = new BoundingBox();
            foreach (var block in primaryGrid.CubeBlocks)
                foreach (var name in block.ConfigNames())
                {
                    if (!name.StartsWithICase(ReservedSpacePrefix)) continue;
                    var args = name.Substring(ReservedSpacePrefix.Length).Trim().Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                    var box = PartDummyUtils.ParseReservedSpace(MyDefinitionManager.Static.GetCubeSize(primaryGrid.GridSizeEnum), block, args,
                        Logger.Warning);
                    box.Box.Max += (Vector3I)block.Min;
                    box.Box.Min += (Vector3I)block.Min;
                    if (m_reservedSpaces.Count == 0)
                        ReservedSpace = new BoundingBox(box.Box.Min, box.Box.Max);
                    ReservedSpace = ReservedSpace.Include(box.Box.Min);
                    ReservedSpace = ReservedSpace.Include(box.Box.Max);
                    m_reservedSpaces.Add(box);
                }
        }

        private void ComputeMountPoints(MyObjectBuilder_CubeGrid primaryGrid, IEnumerable<MyObjectBuilder_CubeGrid> allGrids, BlockSetInfo info)
        {
            m_mountPoints.Clear();
            foreach (var block in primaryGrid.CubeBlocks)
            {
                foreach (var name in block.ConfigNames())
                {
                    if (!name.StartsWithICase(MountPrefix)) continue;
                    var parts = PartDummyUtils.ConfigArguments(name.Substring(MountPrefix.Length)).ToArray();
                    if (parts.Length < 3) continue;
                    var spec = parts[0].Split(':');
                    if (spec.Length != 2) continue;

                    var mountType = spec[0];
                    var mountPiece = spec[1];
                    var mountName = parts[1];

                    Dictionary<string, PartMount> partsOfType;
                    if (!m_mountPoints.TryGetValue(mountType, out partsOfType))
                        partsOfType = m_mountPoints[mountType] = new Dictionary<string, PartMount>();
                    PartMount mount;
                    if (!partsOfType.TryGetValue(mountName, out mount))
                        mount = partsOfType[mountName] = new PartMount(this, mountType, mountName);

                    var args = new string[parts.Length - 2];
                    for (var i = 2; i < parts.Length; i++)
                        args[i - 2] = parts[i];

                    var pmpb = new PartMountPointBlock(mount);
                    pmpb.Init(block, mountPiece, args);
                    mount.Add(pmpb);
                }
            }

            m_mountPointBlocks.Clear();
            foreach (var mount in MountPoints)
                foreach (var block in mount.Blocks)
                    m_mountPointBlocks[block.AnchorLocation] = block;
        }

        private static readonly HashSet<MyDefinitionId> m_erroredDefinitionIds = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        private void ComputeBlockMap(MyObjectBuilder_CubeGrid primaryGrid, IEnumerable<MyObjectBuilder_CubeGrid> allGrids, BlockSetInfo info)
        {
            m_blocks.Clear();
            info.BlockCountByType.Clear();
            info.ComponentCost.Clear();
            info.PowerConsumptionByGroup.Clear();

            BoundingBox = new BoundingBox((Vector3I)primaryGrid.CubeBlocks[0].Min, (Vector3I)primaryGrid.CubeBlocks[0].Min);

            foreach (var grid in allGrids)
                foreach (var block in grid.CubeBlocks)
                {
                    var blockID = block.GetId();
                    var def = MyDefinitionManager.Static.GetCubeBlockDefinition(blockID);
                    if (grid == primaryGrid)
                    {
                        Vector3I blockMin = block.Min;
                        Vector3I blockMax;
                        BlockTransformations.ComputeBlockMax(block, ref def, out blockMax);
                        BoundingBox = BoundingBox.Include(blockMin);
                        BoundingBox = BoundingBox.Include(blockMax);
                        for (var rangeItr = new Vector3I_RangeIterator(ref blockMin, ref blockMax); rangeItr.IsValid(); rangeItr.MoveNext())
                            m_blocks[rangeItr.Current] = block;
                    }
                    if (def == null)
                    {
                        if (m_erroredDefinitionIds.Add(blockID))
                            Logger.Error("Failed to find definition for block {0}", blockID);
                        continue;
                    }

                    info.BlockCountByType.AddValue(def.Id, 1);

                    foreach (var c in def.Components)
                        info.ComponentCost.AddValue(c.Definition, c.Count);

                    var powerUsage = PowerUtilities.MaxPowerConsumption(def);
                    // if it is off, ignore it.
                    if (Math.Abs(powerUsage.Consumption) > 1e-8 && ((block as MyObjectBuilder_FunctionalBlock)?.Enabled ?? true))
                        info.PowerConsumptionByGroup.AddValue(powerUsage.ResourceGroup, powerUsage.Consumption);
                }
        }
        #endregion


        public virtual string Name => $"DummyStorage{GetHashCode():x}";
    }
}
