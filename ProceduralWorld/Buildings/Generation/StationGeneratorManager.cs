using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Generation
{
    public class StationGeneratorManager : LoggingSessionComponent
    {
        public PartManager PartManager { get; private set; }
        private BuildingDatabase m_database;
        public StationGeneratorManager()
        {
            DependsOn<PartManager>(x => { PartManager = x; });
            DependsOn<BuildingDatabase>(x => { m_database = x; });
        }

        public static readonly Type[] SuppliedDeps = { typeof(StationGeneratorManager) };
        public override IEnumerable<Type> SuppliedComponents => SuppliedDeps;



        public bool GenerateFromSeed(ProceduralConstructionSeed seed, ref ProceduralConstruction construction, int? roomCount = null)
        {
            Ob_ProceduralConstructionSeed dbSeed;
            Ob_ProceduralConstruction dbBlueprint;
            Ob_ProceduralFaction dbFaction;
            if (m_database.TryGetBuildingBlueprint(seed.Seed, out dbSeed, out dbBlueprint)
                && dbSeed != null &&
                m_database.TryGetFaction(dbSeed.FactionSeed, out dbFaction) && dbFaction != null)
            {
                seed = new ProceduralConstructionSeed(new ProceduralFactionSeed(dbFaction), seed.Location, dbSeed);
                if (construction == null)
                    construction = new ProceduralConstruction(RootLogger, seed);
                if (dbBlueprint != null)
                {
                    this.Debug("Cache hit for {0}", seed.Seed);
                    if (construction.Init(PartManager, dbBlueprint))
                        return true;
                    this.Debug("Cache invalidated for {0}.  Could not find: {1}", seed.Seed, string.Join(", ", dbBlueprint.Rooms.Where(x => PartManager.LoadNullable(x.PrefabID) == null)));
                    construction.Clear();
                }
            }
            try
            {
                var watch = new Stopwatch();
                watch.Reset();
                watch.Start();
                if (Settings.DebugGenerationResults)
                    Log(MyLogSeverity.Debug, "Seeded construction\n{0}", seed.ToString());
                if (construction == null)
                    construction = new ProceduralConstruction(RootLogger, seed);
                // Seed the generator
                if (!construction.Rooms.Any())
                {
                    var parts = PartManager.ToList();
                    var part = parts[(int)Math.Floor(parts.Count * seed.DeterministicNoise(1234567))];
                    var room = new ProceduralRoom();
                    room.Init(new MatrixI(Base6Directions.Direction.Forward, Base6Directions.Direction.Up), part);
                    construction.AddRoom(room);
                    if (Settings.DebugGenerationStages || Settings.DebugGenerationResults)
                        this.Debug("Added {0} (number {1}) at {2}.", room.Part.Name, construction.Rooms.Count(), room.BoundingBox.Center);
                }
                var scorePrev = construction.ComputeErrorAgainstSeed();
                var scoreStableTries = 0;
                var fastGrowth = (roomCount / 3) ?? (1 + (int)Math.Sqrt(seed.Population / 10f));
                var absoluteRoomsRemain = fastGrowth * 3;
                var gen = new StationGenerator(this, construction);
                while (absoluteRoomsRemain-- > 0)
                {
                    var currentRoomCount = construction.Rooms.Count();
                    if (roomCount.HasValue && currentRoomCount >= roomCount.Value) break;
                    if (!roomCount.HasValue && scoreStableTries > 3) break;
                    if (BlockLimit > 0 && construction.BlockSetInfo.BlockCountByType.Sum(x => x.Value) >= BlockLimit)
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
                    if (Settings.DebugGenerationResults)
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
                        if (Settings.DebugGenerationResults)
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
                    if (Settings.DebugGenerationResults)
                        if (remainingMounts > 0)
                            Log(MyLogSeverity.Debug, "Now there are {0} remaining mounts.  Reason: {1}", remainingMounts, outOfOptions ? "Out of options" : "Out of tries");
                        else
                            Log(MyLogSeverity.Debug, "Sucessfully closed all mount points");
                }
                if (Settings.DebugGenerationResultsError)
                {
                    using (this.IndentUsing())
                        construction.ComputeErrorAgainstSeed(this.Debug);
                }

                var location = MatrixD.CreateFromQuaternion(seed.Orientation);
                location.Translation = seed.Location;

                var msg = $"Added {construction.Rooms.Count()} rooms; generated in {watch.Elapsed}";
                Log(MyLogSeverity.Debug, msg);
                m_database.StoreBuildingBlueprint(construction);
                return true;
            }
            catch (ArgumentException e)
            {
                Log(MyLogSeverity.Error, "Failed to generate station.\n{0}", e.ToString());
                return false;
            }
        }

        public bool GenerateFromSeedAndRemap(ProceduralConstructionSeed seed, ref ProceduralConstruction construction, out ConstructionCopy grids, int? roomCount = null)
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
                grids = GridCreator.RemapAndBuild(construction);
                Log(MyLogSeverity.Debug, "Added {0} rooms in {1}", construction.Rooms.Count(), watch.Elapsed);
                return true;
            }
            catch (Exception e)
            {
                Log(MyLogSeverity.Error, "Failed to generate station.\n{0}", e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Stations are encouraged to be this many blocks at most.
        /// If this value is non-positive no limit is assumed.
        /// </summary>
        public double BlockLimit { get; private set; } = 0;

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_StationGeneratorManager;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
            BlockLimit = config.BlockLimitMultiplier;
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            return new Ob_StationGeneratorManager() { BlockLimitMultiplier = BlockLimit };
        }
    }

    public class Ob_StationGeneratorManager : Ob_ModSessionComponent
    {
        /// <inheritdoc cref="StationGeneratorManager.BlockLimit"/>
        [DefaultValue(0.0)]
        public double BlockLimitMultiplier = 0.0;
    }
}
