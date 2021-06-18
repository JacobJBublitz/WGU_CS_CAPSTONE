using Microsoft.ML.Data;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketInfo
{
    public class ModelInput
    {
        public const int PREDICTION_LOOKAHEAD = 15;

        private const int RSI_LOOKBACK_PERIOD = 14;

        private const int ROC_SMA_PERIOD = 5;

        private const int MACD_FAST_PERIOD = 12;
        private const int MACD_SLOW_PERIOD = 26;
        private const int MACD_SIGNAL_PERIOD = 9;

        [VectorType(4)]
        public float[] Ema = new float[4];

        public float Crossover = float.NaN;

        public float Rsi = float.NaN;
        [VectorType(4)]
        public float[] Roc = new float[4];
        public float Macd = float.NaN;

        [ColumnName("Label")]
        public float Prediction = float.NaN;

        public static IEnumerable<ModelInput> TransformFromPrices(IEnumerable<IQuote> prices, bool makePrediction)
        {
            var processedPrices = new ModelInput[prices.Count()];
            for (var i = 0; i < processedPrices.Length; ++i)
                processedPrices[i] = new ModelInput();

            var ema = new IEnumerator<EmaResult>[]
            {
                Indicator.GetEma(prices, 5).GetEnumerator(),
                Indicator.GetEma(prices, 15).GetEnumerator(),
                Indicator.GetEma(prices, 21).GetEnumerator(),
                Indicator.GetEma(prices, 50).GetEnumerator()
            };

            var rsi = Indicator.GetRsi(prices, RSI_LOOKBACK_PERIOD).GetEnumerator();

            var roc = new IEnumerator<RocResult>[] {
                Indicator.GetRoc(prices, 5, ROC_SMA_PERIOD).GetEnumerator(),
                Indicator.GetRoc(prices, 15, ROC_SMA_PERIOD).GetEnumerator(),
                Indicator.GetRoc(prices, 21, ROC_SMA_PERIOD).GetEnumerator(),
                Indicator.GetRoc(prices, 50, ROC_SMA_PERIOD).GetEnumerator()
            };

            var macd = Indicator.GetMacd(prices, MACD_FAST_PERIOD, MACD_SLOW_PERIOD, MACD_SIGNAL_PERIOD).GetEnumerator();

            foreach (var (processed, current) in processedPrices.Zip(prices))
            {
                var i = 0;

                i = 0;
                foreach (var emaEnumerator in ema)
                {
                    emaEnumerator.MoveNext();
                    processed.Ema[i++] = ((float?)emaEnumerator.Current.Ema).GetValueOrDefault(float.NaN);
                }
                processed.Crossover = processed.Ema[0] - processed.Ema[3];

                rsi.MoveNext();
                processed.Rsi = ((float?)rsi.Current.Rsi).GetValueOrDefault(float.NaN);

                i = 0;
                foreach (var rocEnumerator in roc)
                {
                    rocEnumerator.MoveNext();
                    processed.Roc[i++] = ((float?)rocEnumerator.Current.RocSma).GetValueOrDefault(float.NaN);
                }

                macd.MoveNext();
                processed.Macd = ((float?)macd.Current.Macd).GetValueOrDefault(float.NaN);
            }

            if (makePrediction)
                foreach (var (processed, (current, future)) in processedPrices.Zip(prices.Zip(prices.Skip(PREDICTION_LOOKAHEAD))))
                    processed.Prediction = ((float)future.Close - (float)current.Close) / (float)current.Close;

            return processedPrices.Where(p =>
                p.Ema.All(v => float.IsFinite(v)) &&
                float.IsFinite(p.Crossover) &&
                float.IsFinite(p.Rsi) &&
                p.Roc.All(v => float.IsFinite(v)) &&
                float.IsFinite(p.Macd) &&
                (!makePrediction || float.IsFinite(p.Prediction))
            );
        }
    }

    public class ModelOutput
    {
        [ColumnName("Score")]
        public float PercentChange = float.NaN;
    }
}
