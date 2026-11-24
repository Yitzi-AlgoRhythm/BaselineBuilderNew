using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.AlgorithmSimulation.Pipelines;
using BaselineBuilderNew.Data.ResultObjects;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.IO.Output;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using Microsoft.Extensions.Hosting;

namespace BaselineBuilderNew
{
    internal class App : BackgroundService
    {
        private readonly IR0Calculator _R0Calculator;
        private readonly IQcCalculator _QcCalculator;
        private readonly IDatabaseReader _dbReader;
        private readonly CovPipeline _covPipeline;
        private readonly MDCusumPipeline _mdCusumPipeline;
        private readonly CusumRawPipeline _cusumRawPipeline;

        private readonly BaselineCSVWriter _writer;

        public App
        (
            IR0Calculator r0Calculator,
            IQcCalculator qcCalculator,
            IDatabaseReader dbReader,
            CovPipeline covPipeline,
            MDCusumPipeline mdCusumPipeline,
            CusumRawPipeline cusumRawPipeline,
            BaselineCSVWriter csvWriter
        )
        {
            _R0Calculator = r0Calculator;
            _QcCalculator = qcCalculator;
            _dbReader = dbReader;
            _covPipeline = covPipeline;
            _mdCusumPipeline = mdCusumPipeline;
            _cusumRawPipeline = cusumRawPipeline;
            _writer = csvWriter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _dbReader.InitAsync();

            Console.WriteLine("Initialized DB.");

            R0Result r0 = await _R0Calculator.CalculateR0();

            Console.WriteLine("Calculated R0:");
            foreach (double variance in r0.R0DiagonalVector)
            {
                Console.WriteLine(variance);
            }

            _QcCalculator.Init(r0);
            QcResult qc = await _QcCalculator.CalculateQc();

            Console.WriteLine("Calculated Qc:");
            foreach (double variableProcessNoise in qc.QcVector)
            {
                Console.WriteLine(variableProcessNoise);
            }

            _covPipeline.InitPipeline(qc, r0);
            await _covPipeline.BeginSimulationAsync();
            CovResult cov = await _covPipeline.GetCovarianceMatrix();

            Console.WriteLine("Calculated COV.");

            _mdCusumPipeline.InitPipeline(qc, r0, cov);
            await _mdCusumPipeline.BeginSimulationAsync();
            MDPercentilesResult mdThresholds = await _mdCusumPipeline.GetMDPercentiles();
            CusumsPercentilesResult cusumThresholds = await _mdCusumPipeline.GetCusumPercentiles();

            Console.WriteLine("Calculated MD & CUSUM Thresholds.");

            _cusumRawPipeline.InitPipeline(qc, r0, cusumThresholds);
            await _cusumRawPipeline.BeginSimulationAsync();
            CusumRawPercentilesResult cusumRawThresholds = await _cusumRawPipeline.GetCusumRawThresholds();

            Console.WriteLine("Calculated CusumRaw Thresholds.");

            _writer
                .AddR0(r0)
                .AddQc(qc)
                .AddCov(cov)
                .AddMDThresholds(mdThresholds)
                .AddCusumThresholds(cusumThresholds)
                .AddCusumRawThresholds(cusumRawThresholds);

            Console.WriteLine("Writing CSV files.");

            _writer.WriteBaselineCSVFiles();
        }
    }
}
