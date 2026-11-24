using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Data.ResultObjects;
using StatsLib;

namespace BaselineBuilderNew.AlgorithmSimulation.Calculators
{
    internal class PercentilesCalculator
    {
        private const int MinimumSamples = 10_000;

        private static readonly double[] Percentiles = { 0.0, 90.0, 99.0, 99.9, 99.99 };

        private TDigest _digest = new();

        private long _count = 0;

        public void RecalculatePercentiles(double value)
        {
            _digest.Add(value);
            _count++;
        }

        public void Reset()
        {
            _digest = new TDigest();
            _count = 0;
        }

        public MDPercentilesResult GetMDPercentileValues()
        {
            double[] thresholds = GetPercentilesGeneric();

            return new MDPercentilesResult
            {
                Percentiles = (double[])Percentiles.Clone(),
                Thresholds = thresholds
            };
        }

        public CusumRawPercentilesResult GetRawPercentileValues()
        {
            double[] thresholds = GetPercentilesGeneric();

            return new CusumRawPercentilesResult()
            {
                Percentiles = (double[])Percentiles.Clone(),
                Thresholds = thresholds
            };
        }

        private double[] GetPercentilesGeneric()
        {
            if (_count < MinimumSamples)
            {
                throw new InvalidOperationException(
                    $"Not enough data: {_count} samples, at least {MinimumSamples} required.");
            }

            double[] thresholds = new double[Percentiles.Length];

            for (int i = 0; i < Percentiles.Length; i++)
            {
                thresholds[i] = _digest.Quantile(Percentiles[i] / 100.0);
            }

            return thresholds;
        }
    }
}
