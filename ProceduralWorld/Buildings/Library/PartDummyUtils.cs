using System;
using System.Collections.Generic;
using System.Linq;
using Equinox.Utils;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRageMath;

namespace Equinox.ProceduralWorld.Buildings.Library
{
    public static class PartDummyUtils
    {
        public const string ArgumentBiasDirection = "B:";
        public const string ArgumentSecondBiasDirection = "B2:";
        public const string ArgumentMountDirection = "D:";
        public const string ArgumentAnchorPoint = "A:";
        public const string ArgumentAdjacencyRule = "AR:";


        public static bool TryParseVector(string data, out Vector3I v)
        {
            var coords = data.Split(':');
            v = default(Vector3I);
            return coords.Length == 3 && int.TryParse(coords[0], out v.X) && int.TryParse(coords[1], out v.Y) && int.TryParse(coords[2], out v.Z);

        }

        public static bool TryParseVector(string data, out Vector3 v)
        {
            var coords = data.Split(':');
            v = default(Vector3I);
            return coords.Length == 3 && float.TryParse(coords[0], out v.X) && float.TryParse(coords[1], out v.Y) && float.TryParse(coords[2], out v.Z);
        }
        public static ReservedSpace ParseReservedSpace(float gridSize, MyObjectBuilder_CubeBlock src, string[] args, Utilities.LoggingCallback log = null)
        {
            var optional = false;
            var shared = false;
            var nSet = false;
            var pSet = false;
            var nExt = Vector3.Zero;
            var pExt = Vector3.Zero;
            foreach (var arg in args)
                if (arg.StartsWithICase("NE:"))
                {
                    Vector3 tmp;
                    if (PartDummyUtils.TryParseVector(arg.Substring(3), out tmp))
                    {
                        nSet = true;
                        nExt = tmp;
                    }
                    else
                        log?.Invoke("Failed to decode negative extent argument \"{0}\"", arg);
                }
                else if (arg.StartsWithICase("PE:"))
                {
                    Vector3 tmp;
                    if (PartDummyUtils.TryParseVector(arg.Substring(3), out tmp))
                    {
                        pSet = true;
                        pExt = tmp;
                    }
                    else
                        log?.Invoke("Failed to decode positive extent argument \"{0}\"", arg);
                }
                else if (arg.Equals("share", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("shared", StringComparison.CurrentCultureIgnoreCase))
                    shared = true;
                else if (arg.Equals("opt", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("optional", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("hint", StringComparison.CurrentCultureIgnoreCase))
                    optional = true;
                else
                    log?.Invoke("Failed to decode argument \"{0}\"", arg);
            if (!nSet || !pSet)
            {
                var sense = src as MyObjectBuilder_SensorBlock;
                if (sense != null)
                {
                    if (!nSet)
                        nExt = (Vector3)sense.FieldMin / gridSize;
                    if (!pSet)
                        pExt = (Vector3)sense.FieldMax / gridSize;
                }
                else
                    log?.Invoke("Isn't a sensor block and isn't fully specified");
            }
            var srcTra = new MatrixI(src.BlockOrientation).GetFloatMatrix();
            nExt = Vector3.TransformNormal(nExt, srcTra);
            pExt = Vector3.TransformNormal(pExt, srcTra);
            return new ReservedSpace() { Box = new BoundingBox(nExt, pExt), IsOptional = optional, IsShared = shared };
        }

        public static IEnumerable<string> ConfigArguments(string args)
        {
            return args.Split(' ').Select(x => x.Trim()).Where(x => x.Length > 0);
        }

        public static IEnumerable<string> ConfigNames(this MyObjectBuilder_CubeBlock block)
        {
            var nA = block.Name;
            var nB = (block as MyObjectBuilder_TerminalBlock)?.CustomName;
            return (new[] { nA, nB }).Where(x => x != null).SelectMany(x => x.Split(new string[] { PartMetadata.MultiUseSentinel }, StringSplitOptions.None)).Select(x => x.Trim()).Where(x => x.Length > 0);
        }
    }
}