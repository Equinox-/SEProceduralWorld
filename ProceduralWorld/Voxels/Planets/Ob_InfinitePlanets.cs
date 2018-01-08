using System;
using System.Xml;
using Equinox.Utils.Session;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.ComponentModel;
using Equinox.ProceduralWorld.Buildings.Generation;
using Equinox.ProceduralWorld.Names;
using Equinox.Utils;
using Equinox.Utils.Random;
using VRage.ObjectBuilders;

namespace Equinox.ProceduralWorld.Voxels.Planets
{
    public class Ob_InfinitePlanets : Ob_ModSessionComponent
    {
        [XmlElement("SystemDesc")]
        public List<Ob_InfinitePlanets_SystemDesc> Systems = new List<Ob_InfinitePlanets_SystemDesc>();

        public double SystemSpacing = 50e6;

        public double SystemProbability = 0.5;

        public double ViewDistance = 75e6;
    }

    public class Ob_InfinitePlanets_SystemDesc
    {
        public double Probability;

        [XmlElement("PlanetDesc")]
        public List<Ob_InfinitePlanets_PlanetDesc> PlanetTypes = new List<Ob_InfinitePlanets_PlanetDesc>();

        [DefaultValue(0)]
        public double MinDistanceFromOrigin = 0;

        public Ob_InfinitePlanets_Range PlanetCount =
            new Ob_InfinitePlanets_Range() { Min = 2, Max = 7 };

        public Ob_InfinitePlanets_Range PlanetSpacing =
            new Ob_InfinitePlanets_Range() { Min = 2000e3, Max = 6000e3, Distribution = Ob_InfinitePlanets_Range_Distribution.Normal };
    }

    public class Ob_InfinitePlanets_PlanetDesc : Ob_InfinitePlanets_BodyDesc
    {
        [XmlElement("MoonDesc")]
        public List<Ob_InfinitePlanets_MoonDesc> MoonTypes = new List<Ob_InfinitePlanets_MoonDesc>();

        public Ob_InfinitePlanets_Range MoonCount =
            new Ob_InfinitePlanets_Range() { Min = 0, Max = 2 };

        public Ob_InfinitePlanets_Range MoonSpacing =
            new Ob_InfinitePlanets_Range() { Min = 50e3, Max = 150e3, Distribution = Ob_InfinitePlanets_Range_Distribution.Normal };

        public Ob_InfinitePlanets_PlanetDesc()
        {
            OrbitLocationDeg = new Ob_InfinitePlanets_Range() { Min = -15, Max = 15 };
        }
    }

    public class Ob_InfinitePlanets_MoonDesc : Ob_InfinitePlanets_BodyDesc
    {

    }

    public class Ob_InfinitePlanets_BodyDesc
    {
        public double Probability;
        public Ob_InfinitePlanets_Range BodyRadius;
        public Ob_InfinitePlanets_Range OrbitRadius = new Ob_InfinitePlanets_Range(){Min=0, Max=1e9};
        public Ob_InfinitePlanets_Range OrbitInclinationDeg = new Ob_InfinitePlanets_Range() { Min = -5, Max = 5 };
        public Ob_InfinitePlanets_Range OrbitLocationDeg =
            new Ob_InfinitePlanets_Range() { Min = 0, Max = 360 };

        public SerializableDefinitionId Generator;
    }

    public class Ob_InfinitePlanets_Range
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
        [DefaultValue(Ob_InfinitePlanets_Range_Distribution.Uniform)]
        public Ob_InfinitePlanets_Range_Distribution Distribution =
            Ob_InfinitePlanets_Range_Distribution.Uniform;

        public double Sample(Random rand)
        {
            double val = (Min + Max) / 2;
            switch (Distribution)
            {
                case Ob_InfinitePlanets_Range_Distribution.Normal:
                    {
                        var sd = StandardDeviation ?? Math.Sqrt((Max - Min) / 10);
                        val = rand.NextNormal((Min + Max) / 2, sd);
                        break;
                    }
                case Ob_InfinitePlanets_Range_Distribution.Exponential:
                    {
                        // quantile = 0.9 -> (Max-Min) = -ln(0.1) / lambda
                        // lambda = -ln(0.1) / (Max-Min)
                        var sd = StandardDeviation ?? (2.302 / (Max - Min));
                        val = rand.NextExponential(sd);
                        break;
                    }
                case Ob_InfinitePlanets_Range_Distribution.Uniform:
                default:
                    val = rand.NextDouble() * (Max - Min) + Min;
                    break;
            }
            return val < Min ? Min : (val > Max ? Max : val);
        }
    }

    public enum Ob_InfinitePlanets_Range_Distribution
    {
        Uniform,
        Normal,
        Exponential
    }
}