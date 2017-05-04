using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ProcBuild
{
    public class MyDesignTool
    {
        private const string DUMMY_DEL = "Dummy del ";

        public static void Process(IMyCubeGrid grid)
        {
            if (grid.CustomName == null || !grid.CustomName.StartsWith("EqProcBuild")) return;
            var ob = grid.GetObjectBuilder(true) as MyObjectBuilder_CubeGrid;
            if (ob == null) return;
            SessionCore.Instance.Logger.Log("Begin processing {0}", grid.CustomName);
            try
            {
                var dummyDel = new List<MyTuple<MyObjectBuilder_CubeBlock, string>>();
                var blockKeep = new List<MyObjectBuilder_CubeBlock>();
                var blockMap = new Dictionary<Vector3I, MyObjectBuilder_CubeBlock>();
                foreach (var block in ob.CubeBlocks)
                {
                    string[] names;
                    if (block is MyObjectBuilder_TerminalBlock)
                        names = new[] { (block as MyObjectBuilder_TerminalBlock).CustomName, block.Name };
                    else
                        names = new[] { block.Name };
                    var mount = false;
                    foreach (var name in names)
                    {
                        if (name == null || !name.StartsWith(DUMMY_DEL)) continue;
                        dummyDel.Add(MyTuple.Create(block, name));
                        mount = true;
                        break;
                    }
                    if (mount) continue;
                    blockMap[block.Min] = block;
                    blockKeep.Add(block);
                }
                SessionCore.Instance.Logger.Log("Found {0} blocks to keep, {1} block mounts to remap", blockKeep.Count, dummyDel.Count);
                foreach (var pair in dummyDel)
                {
                    var block = pair.Item1;
                    MyObjectBuilder_CubeBlock outputBlock = null;
                    var outputDir = Base6Directions.Direction.Forward;
                    foreach (var dir in Base6Directions.EnumDirections)
                    {
                        MyObjectBuilder_CubeBlock tmp;
                        if (!blockMap.TryGetValue(block.Min + Base6Directions.GetIntVector(dir), out tmp)) continue;
                        if (outputBlock != null)
                        {
                            MyAPIGateway.Utilities.ShowMessage("Exporter", "Multiple directions found for " + pair.Item2);
                            SessionCore.Instance.Logger.Log("Multiple directions found for {0}", pair.Item2);
                        }
                        outputBlock = tmp;
                        outputDir = dir;
                    }
                    if (outputBlock != null)
                        outputBlock.Name = "Dummy " + pair.Item2.Substring(DUMMY_DEL.Length) + " D:" + Base6Directions.GetOppositeDirection(outputDir).ToString();
                    else
                    {
                        MyAPIGateway.Utilities.ShowMessage("Exporter", "Failed to find delegated mount point for " + pair.Item2);
                        SessionCore.Log("Failed to find delegated mount point for {0}", pair.Item2);
                    }
                }
                ob.CubeBlocks = blockKeep;

                var defOut = new MyObjectBuilder_PrefabDefinition()
                {
                    Id = new SerializableDefinitionId(typeof(MyObjectBuilder_PrefabDefinition), grid.CustomName),
                    CubeGrids = new MyObjectBuilder_CubeGrid[] { ob }
                };

                var fileName = grid.CustomName + ".sbc";
                SessionCore.Instance.Logger.Log("Saving {0}", fileName);
                MyAPIGateway.Utilities.ShowMessage("Export", "Saving " + fileName);

                var mishMash = new MyObjectBuilder_Definitions()
                {
                    Prefabs = new MyObjectBuilder_PrefabDefinition[] { defOut }
                };
                var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(fileName, typeof(MyDesignTool));
                var obCode = MyAPIGateway.Utilities.SerializeToXML(mishMash);
                obCode = obCode.Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
                writer.Write(Encoding.UTF8.GetBytes(obCode));
                writer.Close();
            }
            catch (Exception e)
            {
                SessionCore.Instance.Logger.Log("Error {0}", e.ToString());
            }
            catch
            {
            }
        }
    }
}
