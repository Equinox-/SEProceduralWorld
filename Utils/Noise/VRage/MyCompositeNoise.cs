using System;
using VRage.Library.Utils;

// All credits go to Keen Software House
// https://github.com/KeenSoftwareHouse/SpaceEngineers/tree/master/Sources/VRage/Noise

// Small edits made for ModAPI compatability by Equinox
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable JoinDeclarationAndInitializer
namespace ProcBuild.Utils.Noise.VRage
{
    public class MyCompositeNoise : IMyModule
    {
        private IMyModule[] m_noises;
        private float[] m_amplitudeScales;
        private float m_normalizationFactor;

        private int m_numNoises;

        // Added seed parameter that lets you make this deterministic.
        public MyCompositeNoise(int numNoises, double startFrequency, int seed = -1)
        {
            if (seed == -1)
                seed = MyRandom.Instance.Next();
            m_numNoises = numNoises;
            m_noises = new IMyModule[m_numNoises];
            m_amplitudeScales = new float[m_numNoises];
            m_normalizationFactor = 2.0f - 1.0f / (float)Math.Pow(2, m_numNoises - 1);

            double frequency = startFrequency;
            var randBase = new Random(seed);
            for (int i = 0; i < m_numNoises; ++i)
            {
                m_amplitudeScales[i] = 1.0f / (float)Math.Pow(2.0f, i);
                m_noises[i] = new MySimplex(seed: randBase.Next(), frequency: frequency);
                frequency *= 2.01f;
            }

        }

        private double NormalizeValue(double value)
        {
            return 0.5 * value / m_normalizationFactor + 0.5;
        }

        public double GetValue(double x)
        {
            double value = 0.0;
            for (int i = 0; i < m_numNoises; ++i)
            {
                value += m_amplitudeScales[i] * m_noises[i].GetValue(x);
            }
            return NormalizeValue(value);
        }

        public double GetValue(double x, double y)
        {
            double value = 0.0;
            for (int i = 0; i < m_numNoises; ++i)
            {
                value += m_amplitudeScales[i] * m_noises[i].GetValue(x, y);
            }
            return NormalizeValue(value);
        }

        public double GetValue(double x, double y, double z)
        {
            double value = 0.0;
            for (int i = 0; i < m_numNoises; ++i)
            {
                value += m_amplitudeScales[i] * m_noises[i].GetValue(x, y, z);
            }
            return NormalizeValue(value);
        }

        public float GetValue(double x, double y, double z, int numNoises)
        {
            double value = 0.0;
            for (int i = 0; i < numNoises; ++i)
            {
                value += m_amplitudeScales[i] * m_noises[i].GetValue(x, y, z);
            }
            return (float)(0.5 * value + 0.5);
        }
    }
}
