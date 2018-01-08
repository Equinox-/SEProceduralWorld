using System.Collections.Generic;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.ProceduralWorld.Manager;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Game
{
    public class ProceduralGridComponent : MyEntityComponentBase
    {
        public readonly ProceduralConstruction Construction;
        public bool ForceDebugDraw { get; set; }
        public override string ComponentTypeDebugString => "ProceduralGridComponent";
        private readonly List<IMyCubeGrid> m_grids;
        public IEnumerable<IMyCubeGrid> GridsInGroup => m_grids;
        public bool IsPersistent { get; private set; }

        public readonly ILogging Logger;
        public ProceduralGridComponent(ProceduralConstruction cc, IEnumerable<IMyCubeGrid> gridsInGroup)
        {
            Logger = cc.Logger.Root().CreateProxy(GetType().Name);
            Construction = cc;
            m_grids = new List<IMyCubeGrid>(gridsInGroup);
            UpdateReadyState();
        }


        public void UpdateReadyState()
        {
            var cReady = IsReady;
            IsReady = m_grids.All(e => MyAPIGateway.Entities.EntityExists(e.EntityId));
            if (!IsReady || cReady) return;
            IsPersistent = false;
            foreach (var grid in m_grids)
                grid.Save = false;
        }
        public bool IsReady { get; private set; }
        
        #region SaveOnChange
        private void OnBlockRemoved(IMySlimBlock mySlimBlock)
        {
            Modified("OnBlockRemoved");
        }

        private void OnBlockIntegrityChanged(IMySlimBlock mySlimBlock)
        {
            Modified("OnBlockIntegrityChanged");
        }

        private void OnBlockAdded(IMySlimBlock mySlimBlock)
        {
            Modified("OnBlockAdded");
        }

        private void OnGridChanged(IMyCubeGrid myCubeGrid)
        {
            Modified("OnGridChanged");
        }

        private void Modified(string source)
        {
            if (!IsReady) return;
            IsPersistent = true;
            foreach (var g in m_grids)
            {
                Logger.Info("Mark {0} for saving.  Source: {1}", g.CustomName, source);
                g.Save = true;
                g.OnGridChanged -= OnGridChanged;
                g.OnBlockAdded -= OnBlockAdded;
                g.OnBlockIntegrityChanged -= OnBlockIntegrityChanged;
                g.OnBlockRemoved -= OnBlockRemoved;
            }
        }
        #endregion

        #region GroupUpdate
        private void OnGridSplit(IMyCubeGrid a, IMyCubeGrid b)
        {
            Modified("OnGridSplit");
            m_grids.Add(b);
            RegisterHandlers(b);
        }

        private void OnEntityClosing(IMyEntity a)
        {
            var grid = a as IMyCubeGrid;
            if (grid == null) return;
            if (m_grids.Remove(grid))
                DeregisterHandlers(grid);
        }

        private void RegisterHandlers(IMyCubeGrid g)
        {
            g.OnGridChanged += OnGridChanged;
            g.OnBlockAdded += OnBlockAdded;
            g.OnBlockIntegrityChanged += OnBlockIntegrityChanged;
            g.OnBlockRemoved += OnBlockRemoved;
            g.OnGridSplit += OnGridSplit;
            g.OnMarkForClose += OnEntityClosing;
        }

        private void DeregisterHandlers(IMyCubeGrid g)
        {
            g.OnGridChanged -= OnGridChanged;
            g.OnBlockAdded -= OnBlockAdded;
            g.OnBlockIntegrityChanged -= OnBlockIntegrityChanged;
            g.OnBlockRemoved -= OnBlockRemoved;
            g.OnGridSplit -= OnGridSplit;
            g.OnMarkForClose -= OnEntityClosing;
        }
        #endregion

        public override void OnAddedToScene()
        {
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();

            foreach (var g in m_grids)
            {
                g.Save = false;
                RegisterHandlers(g);
            }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            foreach (var g in m_grids)
                DeregisterHandlers(g);

            base.OnBeforeRemovedFromContainer();
        }

        private IMyCubeGrid Grid => base.Entity as IMyCubeGrid;

        // ReSharper disable InconsistentNaming
        private Color DebugColorBlocksTotal = Color.Red;
        private Color DebugColorReservedSpaceTotal = Color.Green;
        private static readonly Color DebugColorReservedSpace = Color.Blue;
        private static readonly Color DebugColorReservedSpaceShared = Color.Violet;
        private static readonly Color DebugColorReservedSpaceOptional = Color.Cyan;
        private static readonly Color DebugColorReservedSpaceBoth = Color.HotPink;
        private Color DebugColorMountPoint = Color.Black;
        // ReSharper restore InconsistentNaming
        public void DebugDraw(bool force = false)
        {
            force |= ForceDebugDraw;
            if (!force && !Settings.DebugDraw) return;
            var transform = Grid.WorldMatrix;
            var gridSize = Grid.GridSize;
            foreach (var room in Construction.Rooms)
            {
                if (force || Settings.DebugDrawBlocks)
                {
                    var localAABB = new BoundingBoxD((room.BoundingBox.Min - 0.5f) * gridSize, (room.BoundingBox.Max + 0.5f) * gridSize);
                    MySimpleObjectDraw.DrawTransparentBox(ref transform, ref localAABB, ref DebugColorBlocksTotal, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                }
                if (force || Settings.DebugDrawReservedTotal && room.Part.ReservedSpaces.Any())
                {
                    var temp = Utilities.TransformBoundingBox(room.Part.ReservedSpace, room.Transform);
                    var tmpAABB = new BoundingBoxD((temp.Min - 0.5f) * gridSize, (temp.Max + 0.5f) * gridSize);
                    MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref DebugColorReservedSpaceTotal, MySimpleObjectRasterizer.Wireframe, 1, .02f);
                }
                if (force || Settings.DebugDrawReserved)
                    foreach (var rs in room.Part.ReservedSpaces)
                    {
                        var temp = Utilities.TransformBoundingBox(rs.Box, room.Transform);
                        var tmpAABB = new BoundingBoxD(temp.Min * gridSize, (temp.Max + 1) * gridSize);
                        tmpAABB = tmpAABB.Inflate(-0.02);
                        Color color;
                        if (rs.IsShared && rs.IsOptional)
                            color = DebugColorReservedSpaceBoth;
                        else if (rs.IsShared)
                            color = DebugColorReservedSpaceShared;
                        else if (rs.IsOptional)
                            color = DebugColorReservedSpaceOptional;
                        else
                            color = DebugColorReservedSpace;
                        MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref color, MySimpleObjectRasterizer.Wireframe, 1, .005f);
                    }
                // ReSharper disable once InvertIf
                if (force || Settings.DebugDrawMountPoints)
                    foreach (var mount in room.MountPoints)
                    {
                        foreach (var block in mount.MountPoint.Blocks)
                        {
                            var anchorLoc = (Vector3)block.AnchorLocation;
                            var opposeLoc = (Vector3)(block.AnchorLocation + (block.MountDirection * 2));
                            opposeLoc += 0.5f * Vector3.Abs(Vector3I.One - Vector3I.Abs(block.MountDirection)); // hacky way to get perp. components
                            anchorLoc -= 0.5f * Vector3.Abs(Vector3I.One - Vector3I.Abs(block.MountDirection));

                            var anchor = gridSize * Vector3.Transform(anchorLoc, room.Transform.GetFloatMatrix());
                            var oppose = gridSize * Vector3.Transform(opposeLoc, room.Transform.GetFloatMatrix());
                            var tmpAABB = new BoundingBoxD(Vector3.Min(anchor, oppose), Vector3.Max(anchor, oppose));
                            MySimpleObjectDraw.DrawTransparentBox(ref transform, ref tmpAABB, ref DebugColorMountPoint, MySimpleObjectRasterizer.Solid, 1, .02f);
                        }
                    }
            }
        }
    }
}
