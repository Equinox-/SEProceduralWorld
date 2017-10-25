using System;
using System.Xml;
using Equinox.Utils.Session;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.ComponentModel;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Names;
using Equinox.Utils;
using VRage.ObjectBuilders;

namespace Equinox.ProceduralWorld.Voxels.Planets
{
    public class MyObjectBuilder_InfinitePlanets : MyObjectBuilder_ModSessionComponent
    {
        [XmlElement("SystemDesc")]
        public List<MyObjectBuilder_InfinitePlanets_SystemDesc> Systems = new List<MyObjectBuilder_InfinitePlanets_SystemDesc>();

        public double SystemSpacing = 50e6;

        public double SystemProbability = 0.5;

        public double ViewDistance = 75e6;
    }

    public class MyObjectBuilder_InfinitePlanets_SystemDesc
    {
        public double Probability;

        [XmlElement("PlanetDesc")]
        public List<MyObjectBuilder_InfinitePlanets_PlanetDesc> PlanetTypes = new List<MyObjectBuilder_InfinitePlanets_PlanetDesc>();

        [DefaultValue(0)]
        public double MinDistanceFromOrigin = 0;

        public MyObjectBuilder_InfinitePlanets_Range PlanetCount =
            new MyObjectBuilder_InfinitePlanets_Range() { Min = 2, Max = 7 };

        public MyObjectBuilder_InfinitePlanets_Range PlanetSpacing =
            new MyObjectBuilder_InfinitePlanets_Range() { Min = 2000e3, Max = 6000e3, Distribution = MyObjectBuilder_InfinitePlanets_Range_Distribution.Normal };
    }

    public class MyObjectBuilder_InfinitePlanets_PlanetDesc : MyObjectBuilder_InfinitePlanets_BodyDesc
    {
        [XmlElement("MoonDesc")]
        public List<MyObjectBuilder_InfinitePlanets_MoonDesc> MoonTypes = new List<MyObjectBuilder_InfinitePlanets_MoonDesc>();

        public MyObjectBuilder_InfinitePlanets_Range MoonCount =
            new MyObjectBuilder_InfinitePlanets_Range() { Min = 0, Max = 2 };

        public MyObjectBuilder_InfinitePlanets_Range MoonSpacing =
            new MyObjectBuilder_InfinitePlanets_Range() { Min = 50e3, Max = 150e3, Distribution = MyObjectBuilder_InfinitePlanets_Range_Distribution.Normal };

        public MyObjectBuilder_InfinitePlanets_PlanetDesc()
        {
            OrbitLocationDeg = new MyObjectBuilder_InfinitePlanets_Range() { Min = -15, Max = 15 };
        }
    }

    public class MyObjectBuilder_InfinitePlanets_MoonDesc : MyObjectBuilder_InfinitePlanets_BodyDesc
    {

    }

    public class MyObjectBuilder_InfinitePlanets_BodyDesc
    {
        public double Probability;
        public MyObjectBuilder_InfinitePlanets_Range BodyRadius;
        public MyObjectBuilder_InfinitePlanets_Range OrbitRadius = new MyObjectBuilder_InfinitePlanets_Range(){Min=0, Max=1e9};
        public MyObjectBuilder_InfinitePlanets_Range OrbitInclinationDeg = new MyObjectBuilder_InfinitePlanets_Range() { Min = -5, Max = 5 };
        public MyObjectBuilder_InfinitePlanets_Range OrbitLocationDeg =
            new MyObjectBuilder_InfinitePlanets_Range() { Min = 0, Max = 360 };

        public SerializableDefinitionId Generator;
    }

    public class MyObjectBuilder_InfinitePlanets_Range
    {
        [XmlAttribute("Min")]
        public double Min;
        [XmlAttribute("Max")]
        public double Max;
        [XmlIgnore]
        public double? StandardDeviation;


        [XmlAttribute("SD")]
        [DefaultValue("null")]
        public string StandardDeviationSerial
        {
            get { return StandardDeviation?.ToString() ?? "null"; }
            set
            {
                double result;
                if (double.TryParse(value, out result))
                    StandardDeviation = result;
                else
                    StandardDeviation = null;
            }
        }

        [XmlAttribute("Dist")]
        [DefaultValue(MyObjectBuilder_InfinitePlanets_Range_Distribution.Uniform)]
        public MyObjectBuilder_InfinitePlanets_Range_Distribution Distribution =
            MyObjectBuilder_InfinitePlanets_Range_Distribution.Uniform;

        public double Sample(Random rand)
        {
            double val = (Min + Max) / 2;
            switch (Distribution)
            {
                case MyObjectBuilder_InfinitePlanets_Range_Distribution.Normal:
                    {
                        var sd = StandardDeviation ?? Math.Sqrt((Max - Min) / 10);
                        val = rand.NextNormal((Min + Max) / 2, sd);
                        break;
                    }
                case MyObjectBuilder_InfinitePlanets_Range_Distribution.Exponential:
                    {
                        // quantile = 0.9 -> (Max-Min) = -ln(0.1) / lambda
                        // lambda = -ln(0.1) / (Max-Min)
                        var sd = StandardDeviation ?? (2.302 / (Max - Min));
                        val = rand.NextExponential(sd);
                        break;
                    }
                case MyObjectBuilder_InfinitePlanets_Range_Distribution.Uniform:
                default:
                    val = rand.NextDouble() * (Max - Min) + Min;
                    break;
            }
            return val < Min ? Min : (val > Max ? Max : val);
        }
    }

    public enum MyObjectBuilder_InfinitePlanets_Range_Distribution
    {
        Uniform,
        Normal,
        Exponential
    }
}