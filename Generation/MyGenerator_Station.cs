using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcBuild.Creation;
using ProcBuild.Seeds;
using ProcBuild.Storage;
using ProcBuild.Utils;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;

namespace ProcBuild.Generation
{
    public partial class MyGenerator
    {
        public static bool GenerateFully(MyTuple<Vector4I, Vector3D> key, out MyProceduralConstruction construction, out MyConstructionCopy grids)
        {
            var seed = new MyProceduralConstructionSeed(key.Item2, MyProceduralWorld.Instance.SeedAt(key.Item2), key.Item1.GetHashCode());
            try
            {
                var count = 3 + 7 * seed.Random.NextDouble();

                var watch = new Stopwatch();
                watch.Reset();
                watch.Start();

                construction = new MyProceduralConstruction(seed);
                // Seed the generator
                var parts = SessionCore.Instance.PartManager.ToList();
                var part = parts[(int)Math.Floor(parts.Count * seed.Random.NextDouble())];
                construction.GenerateRoom(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);

                for (var i = 0; i < count; i++)
                    if (!MyGenerator.StepConstruction(construction, (i > count / 2) ? 0 : float.NaN))
                        break;
                // Give it plenty of tries to close itself
                if (true)
                {
                    var remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    var triesToClose = remainingMounts * 2 + 2;
                    if (Settings.Instance.DebugGenerationResults)
                        SessionCore.Log("There are {0} remaining mounts.  Giving it {1} tries to close itself.", remainingMounts, triesToClose);
                    var outOfOptions = false;
                    for (var i = 0; i < triesToClose; i++)
                        if (!MyGenerator.StepConstruction(construction, -10))
                        {
                            outOfOptions = true;
                            break;
                        }
                    remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    if (remainingMounts > 0)
                    {
                        if (Settings.Instance.DebugGenerationResults)
                            SessionCore.Log("Now there are {0} remaining mounts.  Trying without hints. Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                        triesToClose = remainingMounts * 2 + 2;
                        for (var i = 0; i < triesToClose; i++)
                            if (!MyGenerator.StepConstruction(construction, -10, false))
                            {
                                outOfOptions = true;
                                break;
                            }
                    }
                    remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    if (Settings.Instance.DebugGenerationResults)
                        if (remainingMounts > 0)
                            SessionCore.Log("Now there are {0} remaining mounts.  Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                        else
                            SessionCore.Log("Sucessfully closed all mount points");
                }
                if (Settings.Instance.DebugGenerationResults)
                    construction.ComputeErrorAgainstSeed(SessionCore.Log);

                var location = MatrixD.CreateTranslation(key.Item2);

                var generate = watch.Elapsed;
                grids = null;
                var iwatch = new Stopwatch();
                watch.Restart();
                var remapper = new MyRoomRemapper();
                foreach (var room in construction.Rooms)
                {
                    iwatch.Restart();
                    if (grids == null)
                        grids = MyGridCreator.SpawnRoomAt(room, location, remapper);
                    else
                        MyGridCreator.AppendRoom(grids, room, remapper);
                    if (Settings.Instance.DebugGenerationResults)
                        SessionCore.Log("Added room {3} of {0} blocks with {1} aux grids in {2}", room.Part.PrimaryGrid.CubeBlocks.Count, room.Part.Prefab.CubeGrids.Length - 1, iwatch.Elapsed, room.Part.Name);
                }
                var msg = $"Added {construction.Rooms.Count()} rooms; generated in {generate}, added in {watch.Elapsed}";
                SessionCore.Log(msg);
                return true;
            }
            catch (Exception e)
            {
                SessionCore.Log(e.ToString());
                grids = null;
                construction = null;
                return false;
            }
        }
    }
}
