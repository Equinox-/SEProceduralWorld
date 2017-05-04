using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ProcBuild
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class SessionCore : MySessionComponentBase
    {
        public static SessionCore Instance { get; private set; }

        public static void Log(string format, params object[] args)
        {
            if (Instance?.Logger != null)
                Instance.Logger.Log(format, args);
            else
                MyLog.Default?.Log(MyLogSeverity.Info, format, args);
        }

        public static readonly Random RANDOM = new Random();

        public override void LoadData()
        {
            base.LoadData();
        }

        private bool m_attached = false;
        public MyPartManager PartManager { get; private set; }
        public Logging Logger { get; private set; }


        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (MyAPIGateway.Session == null) return;
            if (MyAPIGateway.Session.Player == null) return;
            if (!m_attached)
                Attach();
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (m_attached)
                Detach();
        }

        private void Attach()
        {
            m_attached = true;
            Instance = this;
            Logger = new Logging("ProceduralBuilding.log");
            PartManager = new MyPartManager();
            PartManager.LoadAll();
            foreach (var item in PartManager)
                MyAPIGateway.Utilities.ShowMessage("Parts", item.m_prefab.Id.SubtypeName);

            MyAPIGateway.Utilities.ShowNotification("Attached procedural building");
            MyAPIGateway.Utilities.MessageEntered += CommandDispatcher;
        }

        private void Detach()
        {
            m_attached = false;

            MyAPIGateway.Utilities.MessageEntered -= CommandDispatcher;

            Logger?.Close();
            Logger = null;
            PartManager = null;
            Instance = null;
        }

        private static string FormatMatrix(MatrixI m2)
        {
            var m = m2.GetFloatMatrix();
            return $"{m.M11,5} {m.M12,5} {m.M13,5} {m.Translation.X,5}\r\n{m.M21,5} {m.M22,5} {m.M23,5} {m.Translation.Y,5}\r\n{m.M31,5} {m.M32,5} {m.M33,5} {m.Translation.Z,5}";
        }

        private void CommandDispatcher(string messageText, ref bool sendToOthers)
        {
            if (!MyAPIGateway.Session.IsServer || !messageText.StartsWith("/")) return;
            if (messageText.StartsWith("/export"))
            {
                MyAPIGateway.Entities.GetEntities(null, x =>
                {
                    var grid = x as IMyCubeGrid;
                    if (grid != null)
                        MyDesignTool.Process(grid);
                    return false;
                });
                return;
            }
            if (messageText.StartsWith("/list"))
            {
                try
                {
                    var count = 2;
                    if (messageText.Length > 6)
                        int.TryParse(messageText.Substring(6).Trim(), out count);
                    MyAPIGateway.Utilities.ShowMessage("ProcBuild", "List Mount Points");
                    Stopwatch watch = new Stopwatch();
                    watch.Reset();
                    watch.Start();

                    var a = PartManager.First();
                    var builder = new MyGridBuilder();
                    builder.Add(a, new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
                    for (var i = 0; i < count; i++)
                    {
                        var mountPoints = builder.Rooms.SelectMany(x => x.m_freeMounts.Select(y => MyTuple.Create(x, y))).ToList();
                        var available = new HashSet<MyTuple<MyGridBuilder.RoomInstance, MyPartMount, MyGridBuilder.RoomInstance, MyPartMount>>();
                        foreach (var type in PartManager)
                            foreach (var point in mountPoints)
                                foreach (var other in type.MountPointsOfType(point.Item2.m_mountType))
                                {
                                    var mats = point.Item2.GetTransform(other);
                                    if (mats == null) continue;
                                    foreach (var mat in mats)
                                        available.Add(MyTuple.Create(point.Item1, point.Item2, new MyGridBuilder.RoomInstance(type, MyUtilities.Multiply(mat, point.Item1.Transform)), other));
                                }
                        Logger.Log("Choose from {0} options", available.Count);
                        for (var tri = 0; tri < 10 && available.Any(); tri++)
                        {
                            var result = available.ElementAt(SessionCore.RANDOM.Next(0, available.Count - 1));
                            if (builder.Intersects(result.Item3)) continue;
                            builder.Add(result.Item3);
                            result.Item1.m_freeMounts.Remove(result.Item2);
                            result.Item3.m_freeMounts.Remove(result.Item4);
                            Logger.Log("Added {0} (number {1}) at {2}", result.Item3.m_part.m_prefab.Id.SubtypeName, builder.Rooms.Count(), result.Item3.Transform.Translation);
                            break;
                        }
                    }
                    var generate = watch.Elapsed;
                    watch.Restart();
                    var grid = builder.CubeGrid;
                    var build = watch.Elapsed;
                    grid.PositionAndOrientation = new MyPositionAndOrientation(MyAPIGateway.Session.Player.GetPosition(), Vector3D.Up, Vector3D.Right);
                    watch.Restart();
                    var entity = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(grid) as IMyCubeGrid;
                    if (entity != null)
                        foreach (var b in builder.CubeBlocks)
                            entity.AddBlock(b, false);
                    var add = watch.Elapsed;
                    MyAPIGateway.Utilities.ShowMessage("ProcBuild", "Generated " + builder.Rooms.Count() + " rooms in " + generate + ", built in " + build + ", added in " + add);
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                }
            }
        }
    }
}
