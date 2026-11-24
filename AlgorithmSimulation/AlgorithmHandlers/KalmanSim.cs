using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Data.Result_Objects;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.BaselineData;
using BaselineBuilderNew.Data.ResultObjects;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Options;
using ParameterDataLib;

namespace BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers
{
    internal class KalmanSim
    {
        private readonly IKalmanFilter _kalmanFilter;

        private readonly double _forgettingFactor;

        public KalmanSim(IKalmanFilter kalmanFilter, KalmanData kalmanData)
        {
            _kalmanFilter = kalmanFilter;
            _forgettingFactor = kalmanData.ForgettingFactor;
        }

        public void Init(QcResult qc, R0Result r0)
        {
            _kalmanFilter.Init(qc.QcVector, Matrix<double>.Build.DiagonalOfDiagonalVector(r0.R0DiagonalVector), _forgettingFactor);
        }

        public KalmanFilterResult[] KalmanDelegate(ParameterData[] measurements)
        {
            _kalmanFilter.Predict(measurements);
            _kalmanFilter.ComputeInnovation();
            _kalmanFilter.Update();
            _kalmanFilter.ComputeZScores();
            _kalmanFilter.SageHusa();

            KalmanFilterResult? result = _kalmanFilter.GetStepResult();

            if (result is null)
            {
                return [];
            }

            return [result];
        }
    }
}
