using System;
using System.Linq;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public class PartFromPrefab : PartMetadata
    {
        public MyPrefabDefinition Prefab { get; }

        public PartFromPrefab(PartManager manager, MyPrefabDefinition prefab) : base(manager)
        {
            Prefab = prefab;

            var success = false;
            // Try init from cache.
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(CacheName, typeof(PartFromPrefab)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(CacheName, typeof(PartFromPrefab)))
                    {
                        var xml = reader.ReadToEnd();
                        var ob = MyAPIGateway.Utilities.SerializeFromXML<Ob_Part>(xml);
                        if (ob != null && ob.BuilderVersion == Ob_Part.PartBuilderVersion)
                        {
                            Logger.Info("Loading {0} from cache", Name);
                            Init(ob);
                            success = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warning("Malformed cache for {0}.\n{1}", Name, e);
            }
            // Try init from metadata.  If only this actually worked :/ TODO
            //        if (!success && !string.IsNullOrWhiteSpace(prefab.DisplayNameString))
            //            try
            //            {
            //                var data = Convert.FromBase64String(prefab.DisplayNameString);
            //                var obs = MyAPIGateway.Utilities.SerializeFromBinary<MyObjectBuilder_Part>(data);
            //                if (obs != null)
            //                {
            //                    SessionCore.Log("Loading {0} from description", Name);
            //                    Init(obs);
            //                    success = true;
            //                }
            //            }
            //            catch (Exception e)
            //            {
            //                SessionCore.Log("Malformed description tag for {0}.\n{1}", Name, e);
            //            }

            // Fallback to init from prefab data
            if (!success || Prefab.Initialized)
                InitFromPrefab();
        }

        public bool Initialized { private set; get; } = false;

        public void InitFromPrefab()
        {
            if (Initialized) return;

            var cob = GetObjectBuilder();
            var chash = cob.ComputeHash();
            Initialized = true;
            InitFromGrids(Prefab.CubeGrids[0], Prefab.CubeGrids);
            Logger.Info("Loaded {0} with {1} mount points, {2} reserved spaces, and {3} blocks.  {4} aux grids", Name, MountPoints.Count(), ReservedSpaces.Count(), PrimaryGrid.CubeBlocks.Count, Prefab.CubeGrids.Length - 1);
            foreach (var type in MountPointTypes)
                Logger.Info("    ...of type \"{0}\" there are {1}", type, MountPointsOfType(type).Count());

            var obs = GetObjectBuilder();
            var nhash = obs.ComputeHash();
            if (nhash == chash) return;
            MyAPIGateway.Parallel.StartBackground(ParallelUtilities.WrapAction(() =>
            {
                try
                {
                    Logger.Info("Invalid hash for cached definition of {0}; writing to local storage.  {1} => {2}", Name, chash, nhash);
                    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(CacheName, typeof(PartFromPrefab)))
                    {
                        var xml = MyAPIGateway.Utilities.SerializeToXML(obs);
                        writer.Write(xml);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Write failed.\n{0}", e);
                }
            }, Manager));
        }

        private string CacheName => $"cache_{Prefab.Id.SubtypeName}.xml";

        public MyCubeSize PrimaryCubeSize => PrimaryGrid.GridSizeEnum;

        public MyObjectBuilder_CubeGrid PrimaryGrid
        {
            get
            {
                if (!Initialized) InitFromPrefab();
                return Prefab.CubeGrids[0];
            }
        }

        public override string Name => Prefab.Id.SubtypeName.Substring(PartManager.PREFAB_NAME_PREFIX.Length);

        public override MyObjectBuilder_CubeBlock GetCubeAt(Vector3I pos)
        {
            if (!Initialized) InitFromPrefab();
            return base.GetCubeAt(pos);
        }

        public override void Init(Ob_Part v)
        {
            Initialized = false;
            base.Init(v);
        }
    }
}