using MarketInfo.Viewer.ViewModels;
using NUnit.Framework;
using Prism.Events;
using System.Linq;
using MarketInfo.Viewer.Events;
using System.Collections.Generic;

namespace MarketInfo.Tests
{
    public class ViewModelTests
    {
        private static readonly IEnumerable<string> SYMBOLS = new string[]
        {
            "MSFT",
            "AAPL",
            "AMZN",
            "GM",
            "V",
        };

        private static readonly CompanyProfile MSFT_COMPANY_INFO = new CompanyProfile
        {
            Name = "Microsoft",
            LogoUri = null,
            Ticker = "MSFT"
        };
        private static readonly StockQuote MSFT_QUOTE = new StockQuote
        {
            OpenPrice = 100.0,
            HighPrice = 110.0,
            LowPrice = 95.0,
            CurrentPrice = 105.0,
            LastClosePrice = 90.0
        };

        private IEventAggregator _eventAggregator;
        private StockPriceService _stockPriceService;

        [SetUp]
        public void Setup()
        {
            MockStockPriceService stockPriceService = new MockStockPriceService
            {
                Symbols = SYMBOLS
            };
            stockPriceService.CompanyProfiles["MSFT"] = MSFT_COMPANY_INFO;
            stockPriceService.StockQuotes["MSFT"] = MSFT_QUOTE;

            _eventAggregator = new EventAggregator();
            _stockPriceService = stockPriceService;
        }

        [Test]
        public void MainWindowViewModel_Search()
        {
            var mainWindow = new MainWindowViewModel(_eventAggregator, _stockPriceService);
            mainWindow.RefreshSymbolsDelegateCommand.Execute(); // Load symbols from stock price service

            Assert.That(mainWindow.SearchText, Is.Null.Or.Empty);
            Assert.That(mainWindow.FilteredSymbols, Is.EquivalentTo(SYMBOLS));

            mainWindow.FilterSymbolsCommand.Execute("A");
            Assert.That(mainWindow.FilteredSymbols, Is.EquivalentTo(SYMBOLS.Where(s => s.StartsWith("A"))));

            mainWindow.FilterSymbolsCommand.Execute("MSFT");
            Assert.That(mainWindow.FilteredSymbols, Is.EquivalentTo(SYMBOLS.Where(s => s.StartsWith("MSFT"))));

            // Test case-insensitive matching
            mainWindow.FilterSymbolsCommand.Execute("Aa");
            Assert.That(mainWindow.FilteredSymbols, Is.EquivalentTo(SYMBOLS.Where(s => s.StartsWith("AA"))));

            // Empty
            mainWindow.FilterSymbolsCommand.Execute("");
            Assert.That(mainWindow.FilteredSymbols, Is.EquivalentTo(SYMBOLS));
        }

        [Test]
        public void StockInfoViewModel_CompanyInfo()
        {
            var stockInfo = new StockInfoViewModel(_eventAggregator, _stockPriceService);
            // Select MSFT
            _eventAggregator.GetEvent<TickerSymbolSelectedEvent>().Publish("MSFT");

            Assert.That(stockInfo.StockSymbol, Is.EqualTo("MSFT"));
            Assert.That(stockInfo.CompanyName, Is.EqualTo(MSFT_COMPANY_INFO.Name));
        }

        [Test]
        public void StockInfoViewModel_Quote()
        {
            var stockInfo = new StockInfoViewModel(_eventAggregator, _stockPriceService);
            // Select MSFT
            _eventAggregator.GetEvent<TickerSymbolSelectedEvent>().Publish("MSFT");
            _eventAggregator.GetEvent<StockQuoteEvent>().Publish(MSFT_QUOTE);

            Assert.That(stockInfo.StockColor, Is.EqualTo(StockInfoViewModel.STOCK_POSITIVE_BRUSH));
            Assert.That(stockInfo.StockPrice, Is.EqualTo(string.Format(StockInfoViewModel.STOCK_PRICE_FORMAT, MSFT_QUOTE.CurrentPrice)));
        }
    }
}