using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ProceduralWorld.Buildings.Creation;
using Equinox.ProceduralWorld.Buildings.Library;
using Equinox.ProceduralWorld.Buildings.Seeds;
using Equinox.ProceduralWorld.Buildings.Storage;
using Equinox.Utils.Logging;
using Equinox.Utils.Session;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public class MyStationGeneratorManager : MyLoggingSessionComponent
    {
        public MyPartManager PartManager { get; private set; }
        public MyStationGeneratorManager()
        {
            DependsOn<MyPartManager>(x => { PartManager = x; });
        }

        private static readonly Type[] SuppliesDep = { typeof(MyStationGeneratorManager) };
        public override IEnumerable<Type> SuppliedComponents => SuppliesDep;



        public bool GenerateFromSeed(MyProceduralConstructionSeed seed, ref MyProceduralConstruction construction, int? roomCount = null)
        {
            try
            {
                var watch = new Stopwatch();
                watch.Reset();
                watch.Start();
                if (Settings.Instance.DebugGenerationResults)
                    Log(MyLogSeverity.Debug, "Seeded construction\n{0}", seed.ToString());
                if (construction == null)
                    construction = new MyProceduralConstruction(seed);
                // Seed the generator
                if (!construction.Rooms.Any())
                {
                    var parts = PartManager.Where(x => x.Name.Contains("WFC")).ToList();
                    var part = parts[(int)Math.Floor(parts.Count * seed.DeterministicNoise(1234567))];
                    var room = new MyProceduralRoom();
                    room.Init(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
                    construction.AddRoom(room);
                    if (Settings.Instance.DebugGenerationStages || Settings.Instance.DebugGenerationResults)
                        this.Debug("Added {0} (number {1}) at {2}.", room.Part.Name, construction.Rooms.Count(), room.BoundingBox.Center);
                }
                var scorePrev = construction.ComputeErrorAgainstSeed();
                var scoreStableTries = 0;
                var fastGrowth = (roomCount / 3) ?? (1 + (int)Math.Sqrt(seed.Population / 10f));
                var absoluteRoomsRemain = fastGrowth * 3;
                var gen = new MyStationGenerator(this, construction);
                while (absoluteRoomsRemain-- > 0)
                {
                    var currentRoomCount = construction.Rooms.Count();
                    if (roomCount.HasValue && currentRoomCount >= roomCount.Value) break;
                    if (!roomCount.HasValue && scoreStableTries > 5) break;
                    if (construction.BlockSetInfo.BlockCountByType.Sum(x => x.Value) >= MyAPIGateway.Session.SessionSettings.MaxGridSize * 0.75)
                    {
                        Log(MyLogSeverity.Warning, "Quit because we exceeded the block limit");
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
                        Log(MyLogSeverity.Debug, "There are {0} remaining mounts.  Giving it {1} tries to close itself.", remainingMounts, triesToClose);
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
                            Log(MyLogSeverity.Debug, "Now there are {0} remaining mounts.  Trying without hints. Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
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
                            Log(MyLogSeverity.Debug, "Now there are {0} remaining mounts.  Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                        else
                            Log(MyLogSeverity.Debug, "Sucessfully closed all mount points");
                }
                if (Settings.Instance.DebugGenerationResultsError)
                {
                    using (this.IndentUsing())
                        construction.ComputeErrorAgainstSeed(this.Debug);
                }

                var location = MatrixD.CreateFromQuaternion(seed.Orientation);
                location.Translation = seed.Location;

                var msg = $"Added {construction.Rooms.Count()} rooms; generated in {watch.Elapsed}";
                Log(MyLogSeverity.Debug, msg);
                return true;
            }
            catch (ArgumentException e)
            {
                Log(MyLogSeverity.Error, "Failed to generate station.\n{0}", e.ToString());
                return false;
            }
        }

        public bool GenerateFromSeedAndRemap(MyProceduralConstructionSeed seed, ref MyProceduralConstruction construction, out MyConstructionCopy grids, int? roomCount = null)
        {
            grids = null;
            try
            {
                if (!GenerateFromSeed(seed, ref construction, roomCount))
                {
                    Log(MyLogSeverity.Debug, "Failed to generate from seed");
                    return false;
                }
                var watch = new Stopwatch();
                watch.Restart();
                grids = MyGridCreator.RemapAndBuild(construction);
                Log(MyLogSeverity.Debug, "Added {0} rooms in {1}", construction.Rooms.Count(), watch.Elapsed);
                return true;
            }
            catch (Exception e)
            {
                Log(MyLogSeverity.Error, "Failed to generate station.\n{0}", e.ToString());
                return false;
            }
        }
        public override void LoadConfiguration(MyObjectBuilder_ModSessionComponent configOriginal)
        {
            var config = configOriginal as MyObjectBuilder_StationGeneratorManager;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
        }

        public override MyObjectBuilder_ModSessionComponent SaveConfiguration()
        {
            return new MyObjectBuilder_StationGeneratorManager();
        }
    }

    public class MyObjectBuilder_StationGeneratorManager : MyObjectBuilder_ModSessionComponent
    {
    }
}
