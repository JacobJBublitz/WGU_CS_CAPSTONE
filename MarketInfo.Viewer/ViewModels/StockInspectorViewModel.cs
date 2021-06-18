using MarketInfo.Viewer.Events;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Events;
using Prism.Mvvm;
using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using OxyPlot.Annotations;
using Microsoft.ML;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MarketInfo.Viewer.ViewModels
{
    public class StockInspectorViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly StockPriceService _stockPriceService;

        private MLContext _mlContext = new MLContext();
        private PredictionEngine<ModelInput, ModelOutput> _mlPredictionModel;

        private string _symbol;

        private readonly CandleStickSeries _priceSeries = new CandleStickSeries
        {
            IncreasingColor = OxyColors.Green,
            DecreasingColor = OxyColors.Red,
            DataFieldX = "Time",
            DataFieldOpen = "Open",
            DataFieldHigh = "High",
            DataFieldLow = "Low",
            DataFieldClose = "Close",
            TrackerFormatString = "High: {2:0.00}\nLow: {3:0.00}\nOpen: {4:0.00}\nClose: {5:0.00}"
        };
        private readonly LineSeries _predictedPriceSeries = new LineSeries
        {
            Title = "Predicted Future Price",
            Color = OxyColors.Blue
        };

        private readonly LineSeries _ema8Series = new LineSeries { Title = "EMA 8" };
        private readonly LineSeries _ema34Series = new LineSeries { Title = "EMA 34" };
        private readonly AreaSeries _volumeSeries = new AreaSeries { Title = "Volume" };
        private readonly LineSeries _rsiSeries = new LineSeries { Title = "RSI" };

        public PlotModel PriceModel { get; private set; } = new PlotModel { IsLegendVisible = true };
        public PlotModel AuxilaryModel { get; private set; } = new PlotModel { IsLegendVisible = true };

        private StockPriceRange _selectedChartRange = StockPriceRange.DAY;
        public StockPriceRange SelectedChartRange
        {
            get { return _selectedChartRange; }
            set { SetProperty(ref _selectedChartRange, value); UpdateChart(); }
        }

        public StockInspectorViewModel(IEventAggregator ea, StockPriceService stockPriceService)
        {
            _eventAggregator = ea;
            _stockPriceService = stockPriceService;

            using (var stream = new FileStream("Model.zip", FileMode.Open))
            {
                var mlPipeline = _mlContext.Model.Load(stream, out DataViewSchema inputSchema);
                _mlPredictionModel = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlPipeline);
            }

            PriceModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom });
            PriceModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Price (USD)" });
            PriceModel.Series.Add(_priceSeries);
            PriceModel.Series.Add(_predictedPriceSeries);
            PriceModel.Series.Add(_ema8Series);
            PriceModel.Series.Add(_ema34Series);

            AuxilaryModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, Key = "Time" });
            AuxilaryModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Key = "Volume", Title = "Volume" });
            AuxilaryModel.Axes.Add(new LinearAxis { Position = AxisPosition.Right, Key = "RSI", Title="RSI", Minimum = 0, Maximum = 100 });

            _volumeSeries.XAxisKey = "Time";
            _volumeSeries.YAxisKey = "Volume";
            AuxilaryModel.Series.Add(_volumeSeries);

            _rsiSeries.XAxisKey = "Time";
            _rsiSeries.YAxisKey = "RSI";
            AuxilaryModel.Series.Add(_rsiSeries);

            
            AuxilaryModel.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 75,
                Color = OxyColors.Red,
                Text = "Overbought > 70%",
                YAxisKey = "RSI",
                XAxisKey = "Time",
            });
            AuxilaryModel.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 25,
                Color = OxyColors.Green,
                Text = "Oversold < 30%",
                YAxisKey = "RSI",
                XAxisKey = "Time"
            });
            

            ea.GetEvent<TickerSymbolSelectedEvent>().Subscribe(UpdateData);
        }

        private void UpdateData(string symbol)
        {
            _symbol = symbol;

            UpdateChart();
        }

        private async void UpdateChart()
        {
            var quote = await _stockPriceService.GetQuoteAsync(_symbol);
            if (quote != null)
            {
                _eventAggregator.GetEvent<StockQuoteEvent>().Publish(quote);
            }

            var items = new List<StockPrice>();
            await foreach (var price in _stockPriceService.GetPricesAsync(_symbol, SelectedChartRange))
            {
                items.Add(price);
            }

            if (items.Count >= 108)
            {
                var ema8 = Indicator.GetEma(items, 8);
                _ema8Series.ItemsSource = ema8
                    .Where(v => v.Ema != null)
                    .Select(v => new DataPoint(DateTimeAxis.ToDouble(v.Date), (double)v.Ema));
            }
            else
            {
                _ema8Series.ItemsSource = new List<DataPoint>();
            }

            if (items.Count >= 134)
            {
                var ema34 = Indicator.GetEma(items, 34);
                _ema34Series.ItemsSource = ema34
                    .Where(v => v.Ema != null)
                    .Select(v => new DataPoint(DateTimeAxis.ToDouble(v.Date), (double)v.Ema));
            }
            else
            {
                _ema34Series.ItemsSource = new List<DataPoint>();
            }

            if (items.Count >= 114)
            {
                var rsi = Indicator.GetRsi(items);
                _rsiSeries.ItemsSource = rsi
                    .Where(v => v.Rsi != null)
                    .Select(v => new DataPoint(DateTimeAxis.ToDouble(v.Date), (double)v.Rsi));
            }
            else
            {
                _rsiSeries.ItemsSource = new List<DataPoint>();
            }

            _priceSeries.ItemsSource = items
                .Select(price => new HighLowItem(DateTimeAxis.ToDouble(price.Time), price.High, price.Low, price.Open, price.Close));
            _volumeSeries.ItemsSource = items
                .Select(price => new DataPoint(DateTimeAxis.ToDouble(price.Time), price.Volume));

            try
            {
                await UpdatePrediction();
            }
            catch (BadHistoryException)
            {
                ClearPrediction();
            }

            PriceModel.InvalidatePlot(true);
            AuxilaryModel.InvalidatePlot(true);
        }

        private async Task UpdatePrediction()
        {
            StockPriceRange stockRange = StockPriceRange.DAY;
            StockPriceResolution stockResolution = StockPriceResolution.MINUTE;

            switch (SelectedChartRange)
            {
                case StockPriceRange.DAY:
                    stockRange = StockPriceRange.DAY;
                    stockResolution = StockPriceResolution.MINUTE;
                    break;
                case StockPriceRange.WEEK:
                    stockRange = StockPriceRange.MONTH;
                    stockResolution = StockPriceResolution.HOUR;
                    break;
                case StockPriceRange.MONTH:
                    //stockRange = StockPriceRange.MONTH;
                    //stockResolution = StockPriceResolution.HOUR;
                    //break;
                case StockPriceRange.YEAR_TO_DATE:
                case StockPriceRange.YEAR:
                    stockRange = StockPriceRange.YEAR;
                    stockResolution = StockPriceResolution.DAY;
                    break;
            }

            var items = new List<StockPrice>();
            await foreach (var price in _stockPriceService.GetPricesAsync(_symbol, stockRange, stockResolution))
                items.Add(price);

            var inputs = ModelInput.TransformFromPrices(items, false).TakeLast(ModelInput.PREDICTION_LOOKAHEAD).ToList();

            var lastDate = items.Max(item => item.Time);

            var predictions = new List<DataPoint>();
            var i = 0;
            foreach (var (input, price) in inputs.Zip(items.TakeLast(inputs.Count)))
            {
                var output = _mlPredictionModel.Predict(input);
                var predictedPrice = price.Close + (output.PercentChange) * price.Close;

                var date = price.Time;
                switch (stockResolution)
                {
                    case StockPriceResolution.MINUTE:
                        date = lastDate.AddMinutes(i);
                        break;
                    case StockPriceResolution.HOUR:
                        date = lastDate.AddHours(i);
                        break;
                    case StockPriceResolution.DAY:
                        date = lastDate.AddDays(i);
                        break;
                }
                i += 1;

                predictions.Add(DateTimeAxis.CreateDataPoint(date, predictedPrice));
            }

            _predictedPriceSeries.ItemsSource = predictions;
        }

        private void ClearPrediction()
        {
            _predictedPriceSeries.ItemsSource = null;
            PriceModel.InvalidatePlot(true);
        }
    }
}
