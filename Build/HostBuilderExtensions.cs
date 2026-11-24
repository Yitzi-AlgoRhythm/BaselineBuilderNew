using System;
using System.Collections.Generic;
using System.Text;
using AlgoLib.Algorithms.Abstractions;
using AlgoLib.Algorithms.Implementations;
using BaselineBuilderNew.AlgorithmSimulation.AlgorithmHandlers;
using BaselineBuilderNew.AlgorithmSimulation.Calculators;
using BaselineBuilderNew.AlgorithmSimulation.Pipelines;
using BaselineBuilderNew.Config.Builders;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.IO.Input.Abstractions;
using BaselineBuilderNew.IO.Input.Implementations;
using BaselineBuilderNew.IO.Output;
using BaselineBuilderNew.KalmanFilterBaseline.Abstractions;
using BaselineBuilderNew.KalmanFilterBaseline.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoLib;
using ThpUtils.Naming;

namespace BaselineBuilderNew.Build
{
    internal static class HostBuilderExtensions
    {
        public static void AddAppServices(this HostApplicationBuilder builder)
        {
            builder.Services.AddMongoLib(builder.Configuration);
            builder.Services.AddNaming(builder.Configuration);

            builder.Services.Configure<KafkaSettings>(
                builder.Configuration.GetSection(nameof(KafkaSettings)));

            builder.Services.Configure<BaselineSettings>(
                builder.Configuration.GetSection(nameof(BaselineSettings)));

            builder.Services.Configure<CalcSettings>(
                builder.Configuration.GetSection(nameof(CalcSettings)));

            builder.Services.AddSingleton<IDatabaseReader, MongoDBReader>();

            builder.Services.AddSingleton<IR0Calculator, R0Calculator>();
            builder.Services.AddSingleton<IQcCalculator, QcCalculator>();

            builder.Services.AddSingleton<IKalmanFilter, KalmanFilter>();
            builder.Services.AddSingleton<IMahalanobis, Mahalanobis>();
            builder.Services.AddSingleton<ICusum, Cusum>();

            builder.Services.AddSingleton(BaselineDataBuilder.BuildKalmanData);
            builder.Services.AddSingleton(BaselineDataBuilder.BuildCusumData);

            builder.Services.AddSingleton<KalmanSim>();
            builder.Services.AddSingleton<MahalanobisSim>();
            builder.Services.AddSingleton<CusumSim>();

            builder.Services.AddSingleton(sp =>
            {
                NamingData namingData = sp.GetRequiredService<NamingData>();
                return new CovCalculator(namingData.ParameterNames.Length);
            });
            builder.Services.AddSingleton<PercentilesCalculator>();
            builder.Services.AddSingleton<CusumPercentilesCalculator>();

            builder.Services.AddSingleton<CovPipeline>();
            builder.Services.AddSingleton<CusumRawPipeline>();
            builder.Services.AddSingleton<MDCusumPipeline>();

            builder.Services.AddSingleton<BaselineCSVWriter>();

            builder.Services.AddHostedService<App>();
        }
    }
}