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
        private RationalQuadraticRegression rqrOtherMainFast;
        private RationalQuadraticRegression rqrOtherMainSlow;
        private RationalQuadraticRegression rqrMainUSD;
        private AverageTrueRange ATRFast;
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
        int period = 5;
        int periodSlow = 30;
        decimal fastTheta = 0.01m;
        decimal slowTheta = 0.005m;

        public override void Initialize()
        {
            var backtestStart = GetParameter("backtest-start", "2024-11-14");
            var backtestEnd = GetParameter("backtest-end", "2024-11-15");
            if (DateTime.TryParse(backtestStart, out var dtBacktestStart) && DateTime.TryParse(backtestEnd, out var dtBacktestEnd))
            {
                Market.Add("pulsechain", 369);
                SetStartDate(dtBacktestStart);
                SetEndDate(dtBacktestEnd);
            }
            else
            {
                SetStartDate(2024, 11, 14);
                SetEndDate(2024, 11, 15);
            }
            
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
            SetCash(0);
            SetCash(MAIN, GetParameter("cash-main", 2000000m));
            SetCash(OTHER, GetParameter("cash-other", 100000m));
            cashMain = Portfolio.CashBook[MAIN];
            cashOther = Portfolio.CashBook[OTHER];
            
            var rqrPeriod = GetParameter("rqr-period", 10);
            var rqrLookback = GetParameter("rqr-lookback", 10);
            var rqrLookbackFast = GetParameter("rqr-period-fast", rqrLookback / 2);
            var rqrLookbackSlow = GetParameter("rqr-period-slow", rqrLookbackFast * 3);
            var rqrWeight = GetParameter("rqr-weight", 1.0);
            period = GetParameter("period", period);
            periodSlow = period * 3;
            fastTheta = GetParameter("fast-theta", fastTheta);
            slowTheta = GetParameter("slow-theta", slowTheta);
            
            Transactions.MarketOrderFillTimeout = TimeSpan.FromMinutes(2);
            rqrOtherMainFast = new RationalQuadraticRegression(rqrPeriod, rqrLookbackFast, rqrWeight);
            rqrOtherMainSlow = new RationalQuadraticRegression(rqrPeriod, rqrLookbackSlow, rqrWeight);
            rqrMainUSD = new RationalQuadraticRegression(rqrPeriod, rqrLookback, rqrWeight);
            
            rqrOtherMainSlow.Window.Size = 3;
            rqrOtherMainFast.Window.Size = 5;
            rqrMainUSD.Window.Size = 5;
            RegisterIndicator(symbolOtherMain, rqrOtherMainFast, TimeSpan.FromMinutes(period));
            RegisterIndicator(symbolOtherMain, rqrOtherMainSlow, TimeSpan.FromMinutes(periodSlow));
            RegisterIndicator(cryptoMainUSD.Symbol, rqrMainUSD, TimeSpan.FromMinutes(period));
            ATRFast = ATR(symbolOtherMain, 14, MovingAverageType.Simple, Resolution.Minute);

            SetWarmUp(rqrLookbackSlow + rqrPeriod);
            
            var chart = new Chart("PriceChart");
            AddChart(chart);
            var OtherMainCandleSeries = new CandlestickSeries(PairOtherMain, OTHER);
            var rqrOtherMainLineFast = new Series(rqrOtherMainFast.Name, SeriesType.Line, "$", Color.Orange);
            var rqrOtherMainLineSlow = new Series(rqrOtherMainSlow.Name, SeriesType.Line, "$", Color.DarkOrange);
            chart.AddSeries(new Series("Bullish", SeriesType.Scatter, "$", Color.Aqua, ScatterMarkerSymbol.Triangle));
            chart.AddSeries(new Series("Bearish", SeriesType.Scatter, "$", Color.Purple, ScatterMarkerSymbol.TriangleDown));
        
            chart.AddSeries(OtherMainCandleSeries);
            chart.AddSeries(rqrOtherMainLineFast);
            chart.AddSeries(rqrOtherMainLineSlow);
            PlotIndicator("PriceChart", rqrOtherMainFast, rqrOtherMainSlow);

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
            
            /*rqrOtherMainFast.Update(Time, bar.Close);
            var slowClose = Time.Minute % periodSlow == 0;
            if (slowClose)
                rqrOtherMainSlow.Update(Time, bar.Close);*/

            Plot("PriceChart", PairOtherMain, bar);
            

            if (IsWarmingUp || !rqrOtherMainFast.IsReady || !rqrOtherMainSlow.IsReady) return;
            OrderTicket order = null;
            var price = Securities[PairOtherMain].Price;
            var isFastRising = rqrOtherMainFast.IsBullish(fastTheta);  // Fast RQR trend change detection
            var isFastFalling = rqrOtherMainFast.IsBearish(fastTheta);
            var isSlowRising = rqrOtherMainSlow.IsBullish(slowTheta); // Slow RQR for stable trend confirmation
            var isSlowFalling = rqrOtherMainSlow.IsBearish(slowTheta);
            var isBullish = isFastRising || isSlowRising;
            var isBearish = isFastFalling || isSlowFalling;
            wasBullish = rqrOtherMainFast.WasBullish(1, fastTheta) || rqrOtherMainSlow.WasBullish(1, slowTheta);
            wasBearish = rqrOtherMainFast.WasBearish(1, fastTheta) || rqrOtherMainSlow.WasBearish(1, slowTheta);

            if (isBullish && wasBearish)
                Plot("PriceChart", "Bullish", rqrOtherMainFast.Current.Value);
            else if (isBearish && wasBullish)
                Plot("PriceChart", "Bearish", rqrOtherMainFast.Current.Value);

            Log($"{OTHER} @ {price:F5} {MAIN}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}, RQR Slow {rqrOtherMainSlow[2].Value:F5} > {rqrOtherMainSlow[1].Value:F5} > {rqrOtherMainSlow[0].Value:F5}");
            if (isBullish && /*wasBearish &&*/ cashMain.Amount > MAIN_RESERVE)
            {
                var maxMain = Math.Truncate(cashMain.Amount - MAIN_RESERVE);
                var other = Math.Truncate(maxMain / price);
                if (other > 0)
                {
                    Log($"Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");
                    order = Buy(PairOtherMain, other);
                }
            } else if (isBearish && /*wasBullish &&*/ cashOther.Amount > OTHER_RESERVE) {
                var maxOther = Math.Truncate(cashOther.Amount - OTHER_RESERVE);
                if (maxOther > 0)
                {
                    Log($"Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");
                    order = Sell(PairOtherMain, maxOther);
                }
            }
        }

        public override void OnData(Slice data)
        {
            return;
            
            if (!rqrOtherMainFast.IsReady || !rqrOtherMainSlow.IsReady || !ATRFast.IsReady)
                return;

            // Get the current values of RQR and ATR
            decimal fastRQRValue = rqrOtherMainFast.Current.Value;
            decimal slowRQRValue = rqrOtherMainSlow.Current.Value;
            decimal atrValue = ATRFast.Current.Value;

            // Determine RQR Trend Direction
            bool isFastRising = rqrOtherMainFast.IsBullish(fastTheta);  // Fast RQR trend change detection
            bool isFastFalling = rqrOtherMainFast.IsBearish(fastTheta);
            bool isSlowRising = rqrOtherMainSlow.IsBullish(slowTheta); // Slow RQR for stable trend confirmation
            bool isSlowFalling = rqrOtherMainSlow.IsBearish(slowTheta);

            // Dynamic Position Sizing Based on Volatility (ATR)
            decimal maxPositionSize = 1.0m;
            decimal volatilityAdjustment = Math.Min(1.0m, 1.0m / atrValue); // Adjust position size inversely to ATR
            decimal positionSize = maxPositionSize * volatilityAdjustment;
            bool wasBullish = rqrOtherMainFast.WasBullish(1, fastTheta) || rqrOtherMainSlow.WasBullish(1, slowTheta);
            bool wasBearish = rqrOtherMainFast.WasBearish(1, fastTheta) || rqrOtherMainSlow.WasBearish(1, slowTheta);
            // Define Buy and Sell Signals
            bool isBullish = isFastRising || isSlowRising; // Buy if fast or slow RQR shows upward trend
            bool isBearish = isFastFalling || isSlowFalling; // Sell if fast or slow RQR shows downward trend

            var price = Securities[PairOtherMain].Price;

            if (isBullish && wasBearish)
                Plot("PriceChart", "Bullish", rqrOtherMainFast.Current.Value);
            else if (isBearish && wasBullish)
                Plot("PriceChart", "Bearish", rqrOtherMainFast.Current.Value);

            Log($"{OTHER} @ {price:F5} {MAIN}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");
            
            if (isBullish && cashMain.Amount > MAIN_RESERVE)
            {
                var maxMain = Math.Truncate(cashMain.Amount - MAIN_RESERVE);
                var other = Math.Truncate(maxMain / price);
                if (other > 0)
                {
                    Log($"Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");
                    Buy(PairOtherMain, other);
                }
            } else if (isBearish && cashOther.Amount > OTHER_RESERVE) {
                var maxOther = Math.Truncate(cashOther.Amount - OTHER_RESERVE);
                if (maxOther > 0)
                {
                    Log($"Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");
                    Sell(PairOtherMain, maxOther);
                }
            }

            /*var holdings = Portfolio[symbolOtherMain].Quantity;
            // Execute Trades Based on Signals
            if (isBullish && holdings <= 0)
            {
                var maxMain = Math.Truncate(cashMain.Amount - MAIN_RESERVE);
                var other = Math.Truncate(maxMain / price);

                Log($"Buying {symbolOtherMain}, RQRFast: {fastRQRValue}, RQRSlow: {slowRQRValue}, ATR: {atrValue}");
                Log($"Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");

                SetHoldings(symbolOtherMain, positionSize);
            }
            else if (isBearish && holdings >= 0)
            {
                var maxOther = Math.Truncate(cashOther.Amount - OTHER_RESERVE);
                Log($"Selling {symbolOtherMain}, RQRFast: {fastRQRValue}, RQRSlow: {slowRQRValue}, ATR: {atrValue}");
                Log($"Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5} @ {price:F5}, RQR {rqrOtherMainFast[2].Value:F5} > {rqrOtherMainFast[1].Value:F5} > {rqrOtherMainFast[0].Value:F5}");

                Liquidate(symbolOtherMain);
            }*/
        }

        public override void OnEndOfDay(Symbol symbol)
        {
            base.OnEndOfDay(symbol);
            /*var security = Securities[symbol.Value];
            var lastData = (TradeBar)security?.GetLastData();
            if (lastData != null)
                Plot("Price", symbol.Value, lastData);*/
            //Log($"EndOfDay {symbol.Value}: {Portfolio.CashBook[symbol]}");
        }

        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();
            Log($"TotalPortfolioValue: {Portfolio.TotalPortfolioValue}");
            Log($"Portfolio: \r\n{Portfolio.CashBook}");
        }
    }
}
