#nullable enable

using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MarketInfo
{
    public abstract class StockPriceService : IDisposable
    {
        public abstract void Dispose();

        public abstract IAsyncEnumerable<string> GetSymbolsAsync();

        public abstract Task<CompanyProfile?> GetCompanyProfileAsync(string symbol);

        public abstract Task<StockQuote?> GetQuoteAsync(string symbol);

        public async IAsyncEnumerable<StockPrice> GetPricesAsync(string symbol, StockPriceRange range = StockPriceRange.MONTH, StockPriceResolution? resolution = null)
        {
            DateTime from;
            DateTime to;

            switch (range)
            {
                case StockPriceRange.DAY:
                    if (resolution == null)
                        resolution = StockPriceResolution.MINUTE;
                    from = DateTime.UtcNow.Date.AddHours(14.5); // 9:30 AM EST (Market open)
                    to = DateTime.UtcNow.Date.AddHours(21.0); // 4:00 PM EST (Market close)
                    break;
                case StockPriceRange.WEEK:
                    if (resolution == null)
                        resolution = StockPriceResolution.HOUR;
                    from = DateTime.Today.Date.AddDays(-7);
                    to = DateTime.Today.Date;
                    break;
                case StockPriceRange.MONTH:
                    if (resolution == null)
                        resolution = StockPriceResolution.HOUR;
                    from = DateTime.Today.Date.AddMonths(-1);
                    to = DateTime.Today.Date;
                    break;
                case StockPriceRange.YEAR:
                    if (resolution == null)
                        resolution = StockPriceResolution.DAY;
                    from = DateTime.Today.Date.AddYears(-1);
                    to = DateTime.Today.Date;
                    break;
                case StockPriceRange.YEAR_TO_DATE:
                    if (resolution == null)
                    {
                        if (DateTime.Today.Month < 4)
                            resolution = StockPriceResolution.HOUR;
                        else
                            resolution = StockPriceResolution.DAY;
                    }
                    from = new DateTime(DateTime.Today.Year, 1, 1);
                    to = DateTime.Today.Date;
                    break;
                default:
                    if (resolution == null)
                        resolution = StockPriceResolution.HOUR;
                    from = DateTime.Today.Date.AddMonths(-1);
                    to = DateTime.Today.Date;
                    break;
            }

            await foreach (var e in GetPricesAsync(symbol, from, to, resolution.GetValueOrDefault(StockPriceResolution.DAY)))
                yield return e;
        }
        
        public abstract IAsyncEnumerable<StockPrice> GetPricesAsync(string symbol, DateTimeOffset from, DateTimeOffset to, StockPriceResolution resolution);
    }

    public enum StockPriceResolution
    {
        MINUTE,
        HOUR,
        DAY
    }

    public enum StockPriceRange
    {
        [Description("Day")]
        DAY,
        [Description("Week")]
        WEEK,
        [Description("Month")]
        MONTH,
        [Description("Year to Date")]
        YEAR_TO_DATE,
        [Description("Year")]
        YEAR
    }

    public class StockPrice : IQuote
    {
        public DateTime Time;
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public long Volume;

        DateTime IQuote.Date => Time;
        decimal IQuote.Open => (decimal)Open;
        decimal IQuote.High => (decimal)High;
        decimal IQuote.Low => (decimal)Low;
        decimal IQuote.Close => (decimal)Close;
        decimal IQuote.Volume => Volume;
    }

    public class StockQuote
    {
        public double OpenPrice;
        public double HighPrice;
        public double LowPrice;
        public double CurrentPrice;
        public double LastClosePrice;
        public DateTime Time = DateTime.UtcNow;

        public double Change => CurrentPrice - LastClosePrice;
        public double ChangePercent => (CurrentPrice - LastClosePrice) / LastClosePrice;
    }

    public class CompanyProfile
    {
        public string Name;
        public Uri? LogoUri;
        public string Ticker;
    }
}
