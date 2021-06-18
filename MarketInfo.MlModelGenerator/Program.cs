using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.LightGbm;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarketInfo.MlModelGenerator
{
    class StockDataContext : DbContext
    {
        public DbSet<StockInfo> StockInfo { get; set; }
        public DbSet<StockHistoricalPrice> StockPrices { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {   
            optionsBuilder.UseSqlite("Data Source=mltrainingdata.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StockInfo>()
                .HasKey(i => i.Ticker);
            modelBuilder.Entity<StockHistoricalPrice>()
                .HasKey(i => new { i.Date, i.Ticker });
        }
    }

    class Program
    {
        static void Main()
        {
            var context = new MLContext();
            using var db = new StockDataContext();

            // Concatenate inputs into a single Features column
            var dataProcessPipeline = context.Transforms.Concatenate("Features",
                nameof(ModelInput.Ema),
                nameof(ModelInput.Crossover),
                nameof(ModelInput.Rsi),
                nameof(ModelInput.Roc),
                nameof(ModelInput.Macd)
            ).AppendCacheCheckpoint(context);

            (string name, LightGbmRegressionTrainer value)[] trainers =
            {
                ("LightGbm", context.Regression.Trainers.LightGbm()),
                //("FastTree", context.Regression.Trainers.FastTree()),
                //("FastForest", context.Regression.Trainers.FastForest()),
            };

            Console.WriteLine("... Reading Data");

            var input = db.StockInfo
                .Select(i => i.Ticker).ToList()
                .Select(ticker => db.StockPrices.Where(t => t.Ticker == ticker).OrderBy(t => t.Date).ToList())
                .AsParallel()
                .Select(history => ModelInput.TransformFromPrices(history, true))
                .SelectMany(input => input)
                .ToList();

            var data = context.Data.LoadFromEnumerable(input);

            var results = new List<(string name, ITransformer model, RegressionMetrics metrics)>();
            foreach (var (name, trainer) in trainers)
            {
                var trainingPipeline = dataProcessPipeline.Append(trainer);

                Console.Write($"--> Training with: {name}");
                var (model, metrics) = TrainAndEvaluate(context, trainingPipeline, data);
                Console.WriteLine($" (R^2 = {metrics.RSquared:0.00})");
                results.Add((name, model, metrics));
            }

            var bestModel = results
                .OrderByDescending(result => result.metrics.RSquared)
                .First();

            Console.WriteLine($"*** Best Model: {bestModel.name}");
            PrintMetrics(bestModel.metrics);

            using var stream = new FileStream("Model.zip", FileMode.Create);
            context.Model.Save(bestModel.model, data.Schema, stream);
        }

        private static (ITransformer, RegressionMetrics) TrainAndEvaluate(MLContext context, IEstimator<ITransformer> pipeline, IDataView data) {
            var bestModel = context.Regression.CrossValidate(data, pipeline, numberOfFolds: 1000)
                .OrderByDescending(fold => fold.Metrics.RSquared)
                .First();

            return (bestModel.Model, bestModel.Metrics);
        }

        private static void PrintMetrics(RegressionMetrics metrics)
        {
            Console.WriteLine($"    R Squared:               {metrics.RSquared}");
            Console.WriteLine($"    Mean Absolute Error:     {metrics.MeanAbsoluteError}");
            Console.WriteLine($"    Mean Squared Error:      {metrics.MeanSquaredError}");
            Console.WriteLine($"    Root Mean Squared Error: {metrics.RootMeanSquaredError}");
        }
    }


    public class StockInfo
    {
        public string Ticker { get; set; }
    }

    public class StockHistoricalPrice : IQuote
    {
        public DateTime Date { get; set; }
        public string Ticker { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }

        decimal IQuote.Open => (decimal)Open;

        decimal IQuote.High => (decimal)High;

        decimal IQuote.Low => (decimal)Low;

        decimal IQuote.Close => (decimal)Close;

        decimal IQuote.Volume => Volume;
    }
}
