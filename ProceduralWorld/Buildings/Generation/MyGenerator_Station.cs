using System;
using System.Diagnostics;
using System.Linq;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public partial class MyGenerator
    {
        public static bool GenerateFully(MyProceduralConstructionSeed seed, ref MyProceduralConstruction construction, int roomCount = -1)
        {
            try
            {

                var watch = new Stopwatch();
                watch.Reset();
                watch.Start();
                if(Settings.Instance.DebugGenerationResults)
                    SessionCore.Log("Seeded construction\n{0}", seed.ToString());
                if (construction == null)
                    construction = new MyProceduralConstruction(seed);
                // Seed the generator
                if (!construction.Rooms.Any())
                {
                    var parts = SessionCore.Instance.PartManager.ToList();
                    var part = parts[(int)Math.Floor(parts.Count * seed.DeterministicNoise(1234567))];
                    var room = new MyProceduralRoom();
                    room.Init(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
                    construction.RegisterRoom(room);
                }
                var scorePrev = construction.ComputeErrorAgainstSeed();
                var scoreStableTries = 0;
                var fastGrowth = 1 + (int)Math.Sqrt(seed.Population / 10f);
                var absoluteRoomsRemain = 10;
                var gen = new MyStationGenerator(construction);
                while (absoluteRoomsRemain-- > 0)
                {
                    var currentRoomCount = construction.Rooms.Count();
                    if (roomCount >= 0 && currentRoomCount >= roomCount) break;
                    if (roomCount < 0 && scoreStableTries > 5) break;
                    if (construction.BlockSetInfo.BlockCountByType.Sum(x => x.Value) >= MyAPIGateway.Session.SessionSettings.MaxGridSize * 0.75)
                    {
                        SessionCore.Log("Quit because we exceeded the block limit");
                        break;
                    }
                    if (!gen.StepGeneration(fastGrowth > 0 ? 2 : 0))
                        break;
                    fastGrowth--;
                    var scoreNow = construction.ComputeErrorAgainstSeed();
                    if (scoreNow >= scorePrev)
                        scoreStableTries++;
                    else
                        scoreStableTries = 0;
                    scorePrev = scoreNow;
                }
                // Give it plenty of tries to close itself
                if (true)
                {
                    var remainingMounts = construction.Rooms.SelectMany(x => x.MountPoints).Count(y => y.AttachedTo == null);
                    var triesToClose = remainingMounts * 2 + 2;
                    if (Settings.Instance.DebugGenerationResults)
                        SessionCore.Log("There are {0} remaining mounts.  Giving it {1} tries to close itself.", remainingMounts, triesToClose);
                    var outOfOptions = false;
                    for (var i = 0; i < triesToClose; i++)
                        if (!gen.StepGeneration(-10))
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
                            if (!gen.StepGeneration(-10, false))
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
                if (Settings.Instance.DebugGenerationResultsError)
                    construction.ComputeErrorAgainstSeed(SessionCore.Log);

                var location = MatrixD.CreateFromQuaternion(seed.Orientation);
                location.Translation = seed.Location;

                var msg = $"Added {construction.Rooms.Count()} rooms; generated in {watch.Elapsed}";
                SessionCore.Log(msg);
                return true;
            }
            catch (ArgumentException e)
            {
                SessionCore.Log("Failed to generate station.\n{0}", e.ToString());
#if DEBUG
                throw;
#else
                return false;
#endif
            }
        }

        public static bool GenerateFully(MyProceduralConstructionSeed seed, ref MyProceduralConstruction construction, out MyConstructionCopy grids, int roomCount = -1)
        {
            grids = null;
            try
            {
                if (!GenerateFully(seed, ref construction, roomCount)) return false;
                var watch = new Stopwatch();
                watch.Restart();
                grids = MyGridCreator.RemapAndBuild(construction);
                var msg = $"Added {construction.Rooms.Count()} rooms; added in {watch.Elapsed}";
                SessionCore.Log(msg);
                return true;
            }
            catch (ArgumentException e)
            {
                SessionCore.Log("Failed to generate station.\n{0}", e.ToString());
#if DEBUG
                throw;
#else
                return false;
#endif
            }
        }
    }
}
