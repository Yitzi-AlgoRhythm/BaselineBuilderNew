using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.MongoOutputs;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using Microsoft.Extensions.Options;
using MongoLib.Documents;
using StatsLib;
using ThpUtils.Naming;

namespace BaselineBuilderNew.KalmanFilterBaseline.Implementations
{
    internal class R0Calculator : IR0Calculator
    {
        private const double MinVariance = 1e-9;

        private readonly IDatabaseReader _reader;
        private readonly NamingData _namingData;

        private readonly int _requiredNumOfFlights;

        private const int windowSize = 10;
        private const double quantile = 15;

        private Dictionary<string, MovingStatistics> _variableStats;

        private Dictionary<string, TDigest> _quantileCalculators;

        private Dictionary<string, double> _variances;

        private Dictionary<string, int> _variancesAveraged;

        private Dictionary<string, bool> _variablesFoundInDB;


        private int _flightsProcessed = 1;
        private int _batchesProcessed = 1;


        public R0Calculator(IDatabaseReader reader, NamingData namingData, IOptions<CalcSettings> calcSettings)
        {
            _reader = reader;
            _namingData = namingData;

            _requiredNumOfFlights = calcSettings.Value.RequiredNumOfFlights;

            _quantileCalculators = [];

            _variableStats = [];

            _variances = [];
            _variancesAveraged = [];

            _variablesFoundInDB = [];

            foreach (string variableName in _namingData.ParameterNames)
            {
                _variableStats.Add(variableName, new MovingStatistics(windowSize));
                _quantileCalculators.Add(variableName, new TDigest());
                _variablesFoundInDB.Add(variableName, false);
            }
        }


        public async Task<R0Result> CalculateR0()
        {
            FlightDocument[] flights = await _reader.GetFlightsAsync(_requiredNumOfFlights);

            await ProcessFlightsAsync(flights, RecalculateQuantiles);

            return new R0Result()
            {
                R0DiagonalVector = Vector<double>.Build.DenseOfArray(await CalculateVariances(flights))
            };
        }

        private async Task<double[]> CalculateVariances(FlightDocument[] flights)
        {
            await ProcessFlightsAsync(flights, RecalculateVariances);

            List<double> result = [];

            foreach (string variable in _namingData.ParameterNames)
            {
                double clampedVariance = Math.Max(MinVariance, _variances[variable]);

                result.Add(clampedVariance);
            }

            return result.ToArray();
        }

        private async Task ProcessFlightsAsync(FlightDocument[] flights, Action<NamedSample> processSample)
        {
            foreach (FlightDocument flight in flights)
            {
                Console.WriteLine($"Processing Flight #{_flightsProcessed++}");

                ResetWindows();

                NamedSample[] samples = await _reader.GetInitialSamplesAsync(flight);

                while (samples.Length > 0)
                {
                    Console.WriteLine($"Processing 5 minute batch of samples #{_batchesProcessed++}");
                    Console.WriteLine($"Batch sample count: {samples.Length}");

                    foreach (NamedSample sample in samples)
                    {
                        _variablesFoundInDB[sample.Name] = true;

                        processSample(sample);
                    }

                    samples = await _reader.GetNextSamplesAsync();
                }
            }
        }

        private void ResetWindows()
        {
            foreach (string fieldName in _namingData.ParameterNames)
            {
                _variableStats[fieldName] = new MovingStatistics(windowSize);
            }
        }

        private void RecalculateQuantiles(NamedSample sample)
        {
            _variableStats[sample.Name].Push(sample.Sample.Value);

            if (_variableStats[sample.Name].Count < windowSize)
            {
                return;
            }

            double stdDeviation = _variableStats[sample.Name].PopulationStandardDeviation;
            if (!Double.IsFinite(stdDeviation))
            {
                stdDeviation = 0.0;
            }

            _quantileCalculators[sample.Name].Add(stdDeviation);
        }

        private void RecalculateVariances(NamedSample sample)
        {
            _variableStats[sample.Name].Push(sample.Sample.Value);

            if (_variableStats[sample.Name].Count < windowSize ||
                _variableStats[sample.Name].PopulationStandardDeviation > 
                    _quantileCalculators[sample.Name].Quantile(quantile / 100.0))
            {
                return;
            }

            double variance = _variableStats[sample.Name].PopulationVariance;

            if (!_variances.ContainsKey(sample.Name))
            {
                _variances.Add(sample.Name, variance);
                _variancesAveraged.Add(sample.Name, 1);
            }
            else
            {
                _variancesAveraged[sample.Name]++;
                _variances[sample.Name] += (variance - _variances[sample.Name]) / _variancesAveraged[sample.Name];
            }
        }
    }
}
