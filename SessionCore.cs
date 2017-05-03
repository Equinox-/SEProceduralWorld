using System;
using System.Collections.Generic;
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
using VRageMath;

namespace ProcBuild
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class SessionCore : MySessionComponentBase
    {
        public static SessionCore Instance { get; private set; }

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
            if (messageText.StartsWith("/list"))
            {
                try
                {
                    var count = 2;
                    if (messageText.Length > 6)
                        int.TryParse(messageText.Substring(6).Trim(), out count);
                    MyAPIGateway.Utilities.ShowMessage("ProcBuild", "List Mount Points");
                    Logger.Log("Try spawn");
                    var a = PartManager.First();
                    var builder = new MyGridBuilder();
                    builder.Add(a, new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
                    for (var i = 0; i < count; i++)
                    {
                        var mountPoints = builder.Rooms.SelectMany(x => x.m_freeMounts.Select(y => MyTuple.Create(x, y))).ToList();
                        var available = new HashSet<MyTuple<MyGridBuilder.RoomInstance, MyPartMount, MyGridBuilder.RoomInstance, MyPartMount>>();
                        foreach (var type in PartManager)
                            foreach (var point in mountPoints)
                                foreach (var other in type.MountPoints)
                                {
                                    var mats = point.Item2.GetTransform(other.Value);
                                    if (mats == null) continue;
                                    foreach (var mat in mats)
                                        available.Add(MyTuple.Create(point.Item1, point.Item2, new MyGridBuilder.RoomInstance(type, MyUtilities.Multiply(mat, point.Item1.Transform)), other.Value));
                                }
                        available.RemoveWhere(x => builder.Intersects(x.Item3));
                        var num = available.Count;
                        Logger.Log("Available {0} of {1}", num, mountPoints.Count);
                        if (num <= 0) break;
                        var result = available.ElementAt(SessionCore.RANDOM.Next(0, num));
                        builder.Add(result.Item3);
                        result.Item1.m_freeMounts.Remove(result.Item2);
                        result.Item3.m_freeMounts.Remove(result.Item4);
                        Logger.Log("Added {0} at \r\n{1}", result.Item3.m_part.m_prefab.Id.SubtypeName, FormatMatrix(result.Item3.Transform));
                    }
                    var grid = builder.CubeGrid;
                    grid.PositionAndOrientation = new MyPositionAndOrientation(MyAPIGateway.Session.Player.GetPosition(), Vector3D.Up, Vector3D.Right);
                    MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(grid);
                    MyAPIGateway.Utilities.ShowMessage("ProcBuild", "Didn't die ^^\\_(``/)_/^^");
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                }
            }
        }
    }
}
