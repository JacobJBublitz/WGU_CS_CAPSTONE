using MarketInfo.Viewer.Events;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarketInfo.Viewer.ViewModels
{
    public class StockInfoViewModel : BindableBase
    {
        public static readonly string STOCK_UPDATE_TIME_FORMAT = "f";
        public static readonly string STOCK_PRICE_FORMAT = "{0:F2}";
        public static readonly string STOCK_CHANGE_POSITIVE_FORMAT = "\u25B2 {0:F2}";
        public static readonly string STOCK_CHANGE_NEUTRAL_FORMAT = "- {0:F2}";
        public static readonly string STOCK_CHANGE_NEGATIVE_FORMAT = "\u25BC {0:F2}";
        public static readonly string STOCK_CHANGE_PERCENT_FORMAT = "{0:P2}";

        public static readonly Brush STOCK_POSITIVE_BRUSH = Brushes.Green;
        public static readonly Brush STOCK_NEUTRAL_BRUSH = Brushes.Yellow;
        public static readonly Brush STOCK_NEGATIVE_BRUSH = Brushes.Red;

        private readonly StockPriceService _stockPriceService;

        private ImageSource _companyLogo;
        public ImageSource CompanyLogo
        {
            get { return _companyLogo; }
            set { SetProperty(ref _companyLogo, value); }
        }

        private string _companyName = "Example Company Inc.";
        public string CompanyName
        {
            get { return _companyName; }
            set { SetProperty(ref _companyName, value); }
        }

        private string _stockSymbol = "EXMPL";
        public string StockSymbol
        {
            get { return _stockSymbol; }
            set { SetProperty(ref _stockSymbol, value); }
        }

        private string _stockUpdateTime = DateTime.Now.ToString(STOCK_UPDATE_TIME_FORMAT);
        public string StockUpdateTime
        {
            get { return _stockUpdateTime; }
            set { SetProperty(ref _stockUpdateTime, value); }
        }

        private string _stockPrice = string.Format(STOCK_PRICE_FORMAT, 0.0);
        public string StockPrice
        {
            get { return _stockPrice; }
            set { SetProperty(ref _stockPrice, value); }
        }

        private string _stockChange = string.Format(STOCK_CHANGE_NEUTRAL_FORMAT, 0.0);
        public string StockChange
        {
            get { return _stockChange; }
            set { SetProperty(ref _stockChange, value); }
        }

        private string _stockChangePercent = string.Format(STOCK_CHANGE_PERCENT_FORMAT, 0.0);
        public string StockChangePercent
        {
            get { return _stockChangePercent; }
            set { SetProperty(ref _stockChangePercent, value); }
        }

        private Brush _stockColor = STOCK_NEUTRAL_BRUSH;
        public Brush StockColor
        {
            get { return _stockColor; }
            set { SetProperty(ref _stockColor, value); }
        }

        public StockInfoViewModel(IEventAggregator ea, StockPriceService stockPriceService)
        {
            _stockPriceService = stockPriceService;

            ea.GetEvent<TickerSymbolSelectedEvent>().Subscribe(UpdateCompanyInfo);

            ea.GetEvent<StockQuoteEvent>()
                .Subscribe(quote =>
                {
                    StockUpdateTime = quote.Time.ToString(STOCK_UPDATE_TIME_FORMAT);

                    StockPrice = string.Format(STOCK_PRICE_FORMAT, quote.CurrentPrice);

                    if (quote.Change > 0.01)
                    {
                        // Positive change
                        StockChange = string.Format(STOCK_CHANGE_POSITIVE_FORMAT, quote.Change);
                        StockChangePercent = string.Format(STOCK_CHANGE_PERCENT_FORMAT, quote.ChangePercent);
                        StockColor = STOCK_POSITIVE_BRUSH;
                    }
                    else if (quote.Change < -0.01)
                    {
                        // Negative change
                        StockChange = string.Format(STOCK_CHANGE_NEGATIVE_FORMAT, quote.Change);
                        StockChangePercent = string.Format(STOCK_CHANGE_PERCENT_FORMAT, quote.ChangePercent);
                        StockColor = STOCK_NEGATIVE_BRUSH;
                    }
                    else
                    {
                        // Neutral change
                        StockChange = string.Format(STOCK_CHANGE_NEUTRAL_FORMAT, 0);
                        StockChangePercent = string.Format(STOCK_CHANGE_PERCENT_FORMAT, 0);
                        StockColor = STOCK_NEUTRAL_BRUSH;
                    }
                });
        }

        private async void UpdateCompanyInfo(string symbol)
        {
            var profile = await _stockPriceService.GetCompanyProfileAsync(symbol);
            CompanyName = profile.Name;
            if (profile.LogoUri == null)
                CompanyLogo = new BitmapImage();
            else
                CompanyLogo = new BitmapImage(profile.LogoUri);
            StockSymbol = symbol;
        }
    }
}
