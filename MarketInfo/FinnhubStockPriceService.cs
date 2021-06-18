#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarketInfo
{
    public class FinnhubStockPriceService : StockPriceService
    {
        public static readonly string BASE_URI = "https://finnhub.io/api/v1";
        
        private HttpClient _client = new HttpClient();

        public FinnhubStockPriceService(string token)
        {
            _client.DefaultRequestHeaders.Add("X-Finnhub-Token", token);
        }

        public override void Dispose()
        {
            _client.Dispose();
        }

        public override async IAsyncEnumerable<string> GetSymbolsAsync()
        {
            using var stream = await _client.GetStreamAsync($"{BASE_URI}/stock/symbol?exchange=US");
            using var reader = new JsonTextReader(new StreamReader(stream));

            var arr = await JArray.LoadAsync(reader);

            foreach (var entry in arr)
            {
                yield return entry.Value<string>("symbol");
            }
        }

        public override async Task<CompanyProfile?> GetCompanyProfileAsync(string symbol)
        {
            try
            {
                using var stream = await _client.GetStreamAsync($"{BASE_URI}/stock/profile2?symbol={symbol}");
                using var reader = new JsonTextReader(new StreamReader(stream));

                var result = await JObject.LoadAsync(reader);

                Uri? logoUri = null;
                if (result["logo"]?.Type == JTokenType.Uri)
                    logoUri = result.Value<Uri>("logo");

                return new CompanyProfile
                {
                    Name = result.Value<string>("name"),
                    LogoUri = logoUri,
                    Ticker = result.Value<string>("ticker")
                };
            }
            catch (HttpRequestException e)
            {
                Debug.Fail($"Failed to get company profile for {symbol}", e.Message);
                return null;
            }
        }

        public override async Task<StockQuote?> GetQuoteAsync(string symbol)
        {
            try
            {
                var data = await _client.GetStringAsync($"{BASE_URI}/quote?symbol={symbol}");

                var response = JObject.Parse(data);

                if (!(response["o"]?.Type == JTokenType.Float || response["o"]?.Type == JTokenType.Integer) ||
                    !(response["h"]?.Type == JTokenType.Float || response["h"]?.Type == JTokenType.Integer) ||
                    !(response["l"]?.Type == JTokenType.Float || response["l"]?.Type == JTokenType.Integer) ||
                    !(response["c"]?.Type == JTokenType.Float || response["c"]?.Type == JTokenType.Integer) ||
                    !(response["pc"]?.Type == JTokenType.Float || response["pc"]?.Type == JTokenType.Integer))
                {
                    Debug.Fail($"Response from API is not formatted correctly. ({BASE_URI}/stock/quote?symbol={symbol})");
                    return null;
                }

                return new StockQuote
                {
                    OpenPrice = response.Value<double>("o"),
                    HighPrice = response.Value<double>("h"),
                    LowPrice = response.Value<double>("l"),
                    CurrentPrice = response.Value<double>("c"),
                    LastClosePrice = response.Value<double>("pc"),
                    Time = DateTime.Now
                };
            }
            catch (HttpRequestException e)
            {
                Debug.Fail($"Failed to get quote for stock {symbol}", e.Message);
                return null;
            }
        }

        public override async IAsyncEnumerable<StockPrice> GetPricesAsync(string symbol, DateTimeOffset from, DateTimeOffset to, StockPriceResolution resolution)
        {
            string resolutionStr = "";
            switch (resolution)
            {
                case StockPriceResolution.MINUTE: resolutionStr = "1"; break;
                case StockPriceResolution.HOUR: resolutionStr = "60"; break;
                case StockPriceResolution.DAY: resolutionStr = "D"; break;
            }

            using var stream = await _client.GetStreamAsync($"{BASE_URI}/stock/candle?symbol={symbol}&resolution={resolutionStr}&from={from.ToUnixTimeSeconds()}&to={to.ToUnixTimeSeconds()}");
            using var reader = new JsonTextReader(new StreamReader(stream));

            JObject response = await JObject.LoadAsync(reader);

            if (response["s"]?.Type == JTokenType.String && response.Value<string>("s") != "ok")
            {
                yield break;
            }

            var timeArray = response.Value<JArray>("t");
            var openArray = response.Value<JArray>("o");
            var highArray = response.Value<JArray>("h");
            var lowArray = response.Value<JArray>("l");
            var closeArray = response.Value<JArray>("c");
            var volumeArray = response.Value<JArray>("v");

            for (var i = 0; i < timeArray.Count; ++i)
            {
                yield return new StockPrice
                {
                    Time = DateTimeOffset.FromUnixTimeSeconds(timeArray[i].Value<long>()).LocalDateTime,
                    Open = openArray[i].Value<double>(),
                    High = highArray[i].Value<double>(),
                    Low = lowArray[i].Value<double>(),
                    Close = closeArray[i].Value<double>(),
                    Volume = volumeArray[i].Value<long>()
                };
            }
        }
    }
}
