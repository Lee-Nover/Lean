#region imports
using System;
using QuantConnect.Brokerages;
using QuantConnect.Indicators;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Securities;
using QuantConnect.Securities.Crypto;
using QuantConnect.Orders;
using System.Linq;
using System.Drawing;

#endregion
namespace QuantConnect.Algorithm.CSharp
{
    public class PulsechainRQRAlgorithm : QCAlgorithm
    {
        private Crypto cryptoOtherMain;
        private Crypto cryptoMainUSD;
        private Symbol symbolOtherMain;
        private Symbol symbolMainUSD;
        private RationalQuadraticRegression rqrOtherMain;
        private RationalQuadraticRegression rqrMainUSD;
        private bool wasBullish;
        private bool wasBearish;
        private string MARKET = "pulsechain";
        private string MAIN = "PLS";
        private string OTHER = "PDAI";
        private string PairOtherMain;
        private string PairMainUSD;
        private Cash cashMain;
        private Cash cashOther;
        decimal MAIN_RESERVE = 100000m;
        decimal OTHER_RESERVE = 0m;
        public override void Initialize()
        {
            SetStartDate(2024, 02, 02);
            SetEndDate(2024, 03, 14);
            
            SetBrokerageModel(BrokerageName.Default, AccountType.Cash);
            
            MAIN = GetParameter("base-currency", MAIN);
            OTHER = GetParameter("other-currency", OTHER);
            MAIN_RESERVE = GetParameter("base-reserve", MAIN_RESERVE);
            OTHER_RESERVE = GetParameter("other-reserve", OTHER_RESERVE);
            MARKET = GetParameter("market", MARKET);

            PairOtherMain = OTHER + MAIN;
            PairMainUSD = MAIN + "USD";

            UniverseSettings.Resolution = Resolution.Minute;
            cryptoOtherMain = AddCrypto(PairOtherMain, Resolution.Minute, MARKET);
            cryptoMainUSD = AddCrypto(PairMainUSD, Resolution.Minute, MARKET);
            symbolOtherMain = cryptoOtherMain.Symbol;
            symbolMainUSD = cryptoMainUSD.Symbol;
            SetBenchmark(symbolOtherMain);
            SetCash(MAIN, GetParameter("cash-main", 2000000m));
            SetCash(OTHER, GetParameter("cash-other", 100000m));
            cashMain = Portfolio.CashBook[MAIN];
            cashOther = Portfolio.CashBook[OTHER];
            
            var rqrPeriod = GetParameter("rqr-period", 10);
            var rqrLookback = GetParameter("rqr-lookback", 10);
            var rqrWeight = GetParameter("rqr-weight", 1.0);
            var period = GetParameter("period", 5);
            Transactions.MarketOrderFillTimeout = TimeSpan.FromMinutes(1);
            rqrOtherMain = new RationalQuadraticRegression(rqrPeriod, rqrLookback, rqrWeight);
            rqrMainUSD = new RationalQuadraticRegression(rqrPeriod, rqrLookback, rqrWeight);
            rqrOtherMain.Window.Size = 5;
            rqrMainUSD.Window.Size = 5;
            RegisterIndicator(symbolOtherMain, rqrOtherMain, null);
            RegisterIndicator(cryptoMainUSD.Symbol, rqrMainUSD, null);
            SetWarmUp(rqrPeriod + rqrLookback);
            
            var chart = new Chart("PriceChart");
            AddChart(chart);
            var OtherMainCandleSeries = new CandlestickSeries(PairOtherMain, OTHER);
            var rqrOtherMainLine = new Series(rqrOtherMain.Name, SeriesType.Line, "$", Color.Orange);
            chart.AddSeries(new Series("Bullish", SeriesType.Scatter, "$", Color.Aqua, ScatterMarkerSymbol.Triangle));
            chart.AddSeries(new Series("Bearish", SeriesType.Scatter, "$", Color.Purple, ScatterMarkerSymbol.TriangleDown));
        
            chart.AddSeries(OtherMainCandleSeries);
            chart.AddSeries(rqrOtherMainLine);
            PlotIndicator("PriceChart", rqrOtherMain);
            Consolidate(cryptoMainUSD.Symbol, TimeSpan.FromMinutes(period), OnDataConsolidated);
            Consolidate(cryptoOtherMain.Symbol, TimeSpan.FromMinutes(period), OnDataConsolidated);
        }

        private void OnDataConsolidated(TradeBar bar)
        {
            if (bar.Symbol == cryptoMainUSD.Symbol)
            {
                rqrMainUSD.Update(Time, bar.Close);
                return;
            }
            
            rqrOtherMain.Update(Time, bar.Close);

            Plot("PriceChart", PairOtherMain, bar);
            

            if (IsWarmingUp || !rqrOtherMain.IsReady) return;
            OrderTicket order = null;
            var price = Securities[PairOtherMain].Price;
            var isBullish = rqrOtherMain.IsBullish();
            var isBearish = rqrOtherMain.IsBearish();
            wasBullish = rqrOtherMain.WasBullish(1);
            wasBearish = rqrOtherMain.WasBearish(1);
            if (isBullish && wasBearish)
                Plot("PriceChart", "Bullish", rqrOtherMain.Current.Value);
            else if (isBearish && wasBullish)
                Plot("PriceChart", "Bearish", rqrOtherMain.Current.Value);

            Log($"{OTHER} @ {price:F5} {MAIN}, RQR {rqrOtherMain[2].Value:F5} > {rqrOtherMain[1].Value:F5} > {rqrOtherMain[0].Value:F5}");
            if (isBullish && wasBullish)
            {
                var maxMain = Math.Truncate(cashMain.Amount - MAIN_RESERVE);
                var other = Math.Truncate(maxMain / price);
                if (other > 0)
                {
                    Log($"Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5} @ {price:F5}, RQR {rqrOtherMain[2].Value:F5} > {rqrOtherMain[1].Value:F5} > {rqrOtherMain[0].Value:F5}");
                    order = Buy(PairOtherMain, other);
                }
            } else if (isBearish && wasBearish && cashOther.Amount > OTHER_RESERVE) {
                var maxOther = Math.Truncate(cashOther.Amount - OTHER_RESERVE);
                if (maxOther > 0)
                {
                    Log($"Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5} @ {price:F5}, RQR {rqrOtherMain[2].Value:F5} > {rqrOtherMain[1].Value:F5} > {rqrOtherMain[0].Value:F5}");
                    order = Sell(PairOtherMain, maxOther);
                }
            }
            //if (order != null && order.Status == Orders.OrderStatus.Invalid)
            //    Error($"Order Invalid: {order.SubmitRequest.Response.ErrorMessage}");
            wasBullish = isBullish;
            wasBearish = isBearish;
        }

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice data)
        {
            // all is handled in consolidated handler
        }

        public override void OnEndOfDay(Symbol symbol)
        {
            base.OnEndOfDay(symbol);
            /*var security = Securities[symbol.Value];
            var lastData = (TradeBar)security?.GetLastData();
            if (lastData != null)
                Plot("Price", symbol.Value, lastData);*/
            Log($"EndOfDay {symbol.Value}: {Portfolio.CashBook[symbol]}");
        }

        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();
            Log($"TotalPortfolioValue: {Portfolio.TotalPortfolioValue}");
            Log($"Portfolio: \r\n{Portfolio.CashBook}");
        }
    }
}
