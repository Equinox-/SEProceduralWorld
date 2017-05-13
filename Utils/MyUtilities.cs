using System;
using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ProcBuild.Utils
{
    public static class MyUtilities
    {
        // We average integral sin(pi*x) from 0 to 1.
        public const float SunMovementMultiplier = (float)(2 / Math.PI);

        public static double NextNormal(this Random random, double mu = 0, double sigma = 1)
        {
            // Box-Muller Transform
            double u1 = 0, u2 = 0;
            while (u1 <= double.Epsilon)
            {
                u1 = random.NextDouble();
                u2 = random.NextDouble();
            }

            // Deterministic, but still uniformly represent the two axes
            if (u1 < 0.5D)
                return mu + sigma * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            else
                return mu + sigma * Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
        }

        public static double NextExponential(this Random random, double lambda = 1)
        {
            return lambda * Math.Pow(-Math.Log(random.NextDouble()), lambda);
        }

        public static void AddOrApply<TK, TV>(this Dictionary<TK, TV> dict, TK key, TV val, Func<TV, TV, TV> biFunc)
        {
            TV valCurrent;
            if (!dict.TryGetValue(key, out valCurrent))
            {
                dict[key] = val;
                return;
            }
            dict[key] = biFunc.Invoke(valCurrent, val);
        }

        public static void AddValue<TK>(this Dictionary<TK, MyFixedPoint> dict, TK key, MyFixedPoint val)
        {
            dict[key] = dict.GetValueOrDefault(key, 0) + val;
        }
        public static void AddValue<TK>(this Dictionary<TK, int> dict, TK key, int val)
        {
            dict[key] = dict.GetValueOrDefault(key, 0) + val;
        }
        public static void AddValue<TK>(this Dictionary<TK, float> dict, TK key, float val)
        {
            dict[key] = dict.GetValueOrDefault(key, 0) + val;
        }
        public static void AddValue<TK>(this Dictionary<TK, double> dict, TK key, double val)
        {
            dict[key] = dict.GetValueOrDefault(key, 0) + val;
        }

        public static void AddIfNotNull<T>(this List<T> list, T value) where T : class
        {
            if (value != null)
                list.Add(value);
        }

        public static bool Equals(this MatrixI mat, MatrixI other)
        {
            return mat.Translation == other.Translation && mat.Backward == other.Backward && mat.Right == other.Right && mat.Up == other.Up;
        }

        public static Vector3D Slerp(this Vector3D start, Vector3D end, float percent)
        {
            var dot = start.Dot(end);
            // clamp [-1,1]
            dot = dot < -1 ? -1 : (dot > 1 ? 1 : dot);
            var theta = Math.Acos(dot) * percent;
            var tmp = end - start * dot;
            tmp.Normalize();
            return (start * Math.Cos(theta)) + (tmp * Math.Sin(theta));
        }

        public static Vector3D NLerp(this Vector3D start, Vector3D end, float percent)
        {
            var res = Vector3D.Lerp(start, end, percent);
            res.Normalize();
            return res;
        }

        public delegate void LoggingCallback(string format, params object[] args);

        public static LoggingCallback LogToList(List<string> dest)
        {
            return (x, y) => dest.Add(string.Format(x, y));
        }

        public static bool StartsWithICase(this string s, string arg)
        {
            return s.StartsWith(arg, true, null);
        }

        public static bool IsValidEquinox(this double f)
        {
            return !double.IsNaN(f) && !double.IsInfinity(f);
        }

        public static MatrixD AsMatrixD(this MyPositionAndOrientation posAndOrient)
        {
            //GR: Check for NaN values and remove them (otherwise there will be problems wilth clusters)
            if (posAndOrient.Position.x.IsValidEquinox() == false)
            {
                posAndOrient.Position.x = 0.0f;
            }
            if (posAndOrient.Position.y.IsValidEquinox() == false)
            {
                posAndOrient.Position.y = 0.0f;
            }
            if (posAndOrient.Position.z.IsValidEquinox() == false)
            {
                posAndOrient.Position.z = 0.0f;
            }

            var matrix = MatrixD.CreateWorld(posAndOrient.Position, posAndOrient.Forward, posAndOrient.Up);
            MyUtils.AssertIsValid(matrix);

            var offset = 10.0f;
            // MZ: hotfixed crashing game
            var bbWorld = MyAPIGatewayShortcuts.GetWorldBoundaries != null ? MyAPIGatewayShortcuts.GetWorldBoundaries() : default(BoundingBoxD);
            // clam only if AABB is valid
            if (bbWorld.Max.X > bbWorld.Min.X && bbWorld.Max.Y > bbWorld.Min.Y && bbWorld.Max.Z > bbWorld.Min.Z)
            {
                var resPosition = matrix.Translation;
                if (resPosition.X > bbWorld.Max.X)
                    resPosition.X = bbWorld.Max.X - offset;
                else if (resPosition.X < bbWorld.Min.X)
                    resPosition.X = bbWorld.Min.X + offset;
                if (resPosition.Y > bbWorld.Max.Y)
                    resPosition.Y = bbWorld.Max.Y - offset;
                else if (resPosition.Y < bbWorld.Min.Y)
                    resPosition.Y = bbWorld.Min.Y + offset;
                if (resPosition.Z > bbWorld.Max.Z)
                    resPosition.Z = bbWorld.Max.Z - offset;
                else if (resPosition.Z < bbWorld.Min.Z)
                    resPosition.Z = bbWorld.Min.Z + offset;
                matrix.Translation = resPosition;
            }
            return matrix;
        }

        #region AABB Transforms
        public static BoundingBoxI TransformBoundingBox(BoundingBoxI box, MatrixI matrix)
        {
            Vector3I a, b;
            Vector3I.Transform(ref box.Min, ref matrix, out a);
            Vector3I.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBoxI(Vector3I.Min(a, b), Vector3I.Max(a, b));
        }
        public static BoundingBoxI TransformBoundingBox(BoundingBoxI box, ref MatrixI matrix)
        {
            Vector3I a, b;
            Vector3I.Transform(ref box.Min, ref matrix, out a);
            Vector3I.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBoxI(Vector3I.Min(a, b), Vector3I.Max(a, b));
        }

        public static BoundingBox TransformBoundingBox(BoundingBox box, Matrix matrix)
        {
            Vector3 a, b;
            Vector3.Transform(ref box.Min, ref matrix, out a);
            Vector3.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
        }
        public static BoundingBox TransformBoundingBox(BoundingBox box, ref Matrix matrix)
        {
            Vector3 a, b;
            Vector3.Transform(ref box.Min, ref matrix, out a);
            Vector3.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
        }

        public static BoundingBox TransformBoundingBox(BoundingBox box, MatrixI matrix)
        {
            Vector3 a, b;
            Vector3.Transform(ref box.Min, ref matrix, out a);
            Vector3.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
        }

        public static BoundingBoxD TransformBoundingBox(BoundingBoxD box, MatrixD matrix)
        {
            Vector3D a, b;
            Vector3D.Transform(ref box.Min, ref matrix, out a);
            Vector3D.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBoxD(Vector3D.Min(a, b), Vector3D.Max(a, b));
        }

        public static BoundingBox TransformBoundingBox(BoundingBox box, ref MatrixI matrix)
        {
            Vector3 a, b;
            Vector3.Transform(ref box.Min, ref matrix, out a);
            Vector3.Transform(ref box.Max, ref matrix, out b);
            return new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
        }
        #endregion

        internal static MatrixI Multiply(MatrixI right, MatrixI left)
        {
            MatrixI result;
            result.Backward = left.GetDirection(right.Backward);
            result.Right = left.GetDirection(right.Right);
            result.Up = left.GetDirection(right.Up);
            result.Translation = Vector3I.Transform(right.Translation, left);
            return result;
        }



        public static Color NextColor => colors[colorID++];

        private static int colorID = 0;
        private static readonly Color[] colors = new[]
        {
            Color.MediumSeaGreen, Color.Lavender, Color.DarkViolet, Color.Salmon, Color.SlateBlue, Color.AntiqueWhite, Color.DarkGoldenrod, Color.DarkKhaki, Color.MediumBlue, Color.Magenta, Color.PapayaWhip, Color.Orange, Color.SandyBrown,
            Color.Pink, Color.LightCyan, Color.LightGray, Color.LemonChiffon, Color.LightSkyBlue, Color.Snow, Color.Gold, Color.OldLace, Color.PeachPuff, Color.Brown, Color.Linen, Color.Tomato, Color.MidnightBlue, Color.LightSalmon, Color.LimeGreen,
            Color.PowderBlue, Color.DarkSlateBlue, Color.LightYellow, Color.MediumVioletRed, Color.DarkOliveGreen, Color.Goldenrod, Color.Indigo, Color.DarkCyan, Color.LavenderBlush, Color.Cornsilk, Color.Ivory, Color.Coral, Color.DarkSeaGreen,
            Color.MediumSpringGreen, Color.Azure, Color.Transparent, Color.Orchid, Color.Chartreuse, Color.FloralWhite, Color.Gainsboro, Color.RoyalBlue, Color.CadetBlue, Color.DarkSalmon, Color.DarkMagenta, Color.Beige, Color.Bisque, Color.Plum,
            Color.OrangeRed, Color.Olive, Color.Firebrick, Color.SkyBlue, Color.IndianRed, Color.Fuchsia, Color.CornflowerBlue, Color.DarkOrange, Color.BurlyWood, Color.Moccasin, Color.PaleTurquoise, Color.DeepPink, Color.Yellow, Color.SaddleBrown,
            Color.Tan, Color.MediumSlateBlue, Color.Teal, Color.YellowGreen, Color.Peru, Color.MintCream, Color.Blue, Color.DarkRed, Color.ForestGreen, Color.RosyBrown, Color.SteelBlue, Color.White, Color.DarkOrchid, Color.Gray, Color.Violet,
            Color.Maroon, Color.WhiteSmoke, Color.BlueViolet, Color.DarkSlateGray, Color.MistyRose, Color.SeaGreen, Color.DodgerBlue, Color.OliveDrab, Color.BlanchedAlmond, Color.DarkBlue, Color.HotPink, Color.DarkTurquoise, Color.PaleGreen,
            Color.Khaki, Color.Lime, Color.Honeydew, Color.Aqua, Color.Aquamarine, Color.DimGray, Color.Navy, Color.PaleGoldenrod, Color.Cyan, Color.Purple, Color.LightSeaGreen, Color.GreenYellow, Color.AliceBlue, Color.LightBlue, Color.Red,
            Color.LightPink, Color.Crimson, Color.SpringGreen, Color.Black, Color.Thistle, Color.PaleVioletRed, Color.MediumTurquoise, Color.MediumPurple, Color.Sienna, Color.Chocolate, Color.DeepSkyBlue, Color.SlateGray, Color.LawnGreen,
            Color.SeaShell, Color.MediumOrchid, Color.Wheat, Color.DarkGreen, Color.LightSlateGray, Color.MediumAquamarine, Color.LightSteelBlue, Color.DarkGray, Color.Turquoise, Color.NavajoWhite, Color.LightCoral, Color.LightGoldenrodYellow,
            Color.GhostWhite, Color.LightGreen, Color.Green, Color.Silver
        };
    }
}
