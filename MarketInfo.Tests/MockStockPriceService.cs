using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketInfo.Tests
{
    class MockStockPriceService : StockPriceService
    {
        public IDictionary<string, CompanyProfile> CompanyProfiles = new Dictionary<string, CompanyProfile>();
        public IDictionary<string, StockQuote> StockQuotes = new Dictionary<string, StockQuote>();
        public IEnumerable<string> Symbols = new List<string>();

        public override void Dispose() { }

        public override async Task<CompanyProfile> GetCompanyProfileAsync(string symbol)
        {
            return CompanyProfiles[symbol];
        }

        public override IAsyncEnumerable<StockPrice> GetPricesAsync(string symbol, DateTimeOffset from, DateTimeOffset to, StockPriceResolution resolution)
        {
            throw new NotImplementedException();
        }

        public override async Task<StockQuote> GetQuoteAsync(string symbol)
        {
            return StockQuotes[symbol];
        }

        public override async IAsyncEnumerable<string> GetSymbolsAsync()
        {
            foreach (var symbol in Symbols)
                yield return symbol;
        }
    }
}
