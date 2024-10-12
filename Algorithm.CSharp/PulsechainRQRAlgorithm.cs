#region imports
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using System.Drawing;
    using QuantConnect;
    using QuantConnect.Algorithm.Framework;
    using QuantConnect.Algorithm.Framework.Selection;
    using QuantConnect.Algorithm.Framework.Alphas;
    using QuantConnect.Algorithm.Framework.Portfolio;
    using QuantConnect.Algorithm.Framework.Execution;
    using QuantConnect.Algorithm.Framework.Risk;
    using QuantConnect.Parameters;
    using QuantConnect.Benchmarks;
    using QuantConnect.Brokerages;
    using QuantConnect.Util;
    using QuantConnect.Interfaces;
    using QuantConnect.Algorithm;
    using QuantConnect.Indicators;
    using QuantConnect.Data;
    using QuantConnect.Data.Consolidators;
    using QuantConnect.Data.Custom;
    using QuantConnect.DataSource;
    using QuantConnect.Data.Fundamental;
    using QuantConnect.Data.Market;
    using QuantConnect.Data.UniverseSelection;
    using QuantConnect.Notifications;
    using QuantConnect.Orders;
    using QuantConnect.Orders.Fees;
    using QuantConnect.Orders.Fills;
    using QuantConnect.Orders.Slippage;
    using QuantConnect.Scheduling;
    using QuantConnect.Securities;
    using QuantConnect.Securities.Equity;
    using QuantConnect.Securities.Future;
    using QuantConnect.Securities.Option;
    using QuantConnect.Securities.Forex;
    using QuantConnect.Securities.Crypto;
    using QuantConnect.Securities.Interfaces;
    using QuantConnect.Storage;
    using QCAlgorithmFramework = QuantConnect.Algorithm.QCAlgorithm;
    using QCAlgorithmFrameworkBridge = QuantConnect.Algorithm.QCAlgorithm;
using System.Xml;
using QLNet;
using QuantConnect.Securities.CurrencyConversion;
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
        decimal MAIN_RESERVE = 100000m;
        decimal OTHER_RESERVE = 0m;
        private bool consolidate = true;
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
            cryptoOtherMain = AddCrypto(PairOtherMain, consolidate ? Resolution.Minute : Resolution.Hour, MARKET);
            cryptoMainUSD = AddCrypto(PairMainUSD, consolidate ? Resolution.Minute : Resolution.Hour, MARKET);
            symbolOtherMain = cryptoOtherMain.Symbol;
            symbolMainUSD = cryptoMainUSD.Symbol;
            SetBenchmark(symbolOtherMain);
            SetCash(MAIN, GetParameter("cash-main", 2000000m));
            SetCash(OTHER, GetParameter("cash-other", 100000m));
            var rqrPeriod = GetParameter("rqr-period", 10);
            var rqrLookback = GetParameter("rqr-lookback", 10);
            var rqrWeight = GetParameter("rqr-weight", 1.0);
            
            rqrOtherMain = new RationalQuadraticRegression(rqrPeriod, rqrLookback, rqrWeight);
            rqrMainUSD = new RationalQuadraticRegression(rqrPeriod, rqrLookback, rqrWeight);
            RegisterIndicator(symbolOtherMain, rqrOtherMain, null);
            RegisterIndicator(cryptoMainUSD.Symbol, rqrMainUSD, null);
            SetWarmUp(rqrPeriod + rqrLookback);
            
            /*var chart = new Chart("Price");
            AddChart(chart);
            chart.AddSeries(new Series(rqr.Name, SeriesType.Line, "$", Color.Orange));
            chart.AddSeries(new CandlestickSeries(OTHER, OTHER));*/
            if (consolidate)
            {
                Consolidate(cryptoMainUSD.Symbol, TimeSpan.FromMinutes(5), OnDataConsolidated);
                Consolidate(cryptoOtherMain.Symbol, TimeSpan.FromMinutes(5), OnDataConsolidated);
            }
        }

        private void OnDataConsolidated(TradeBar bar)
        {
            if (bar.Symbol == cryptoMainUSD.Symbol)
            {
                rqrMainUSD.Update(Time, bar.Close);
                return;
            }
            
            rqrOtherMain.Update(Time, bar.Close);
            if (IsWarmingUp || !rqrOtherMain.IsReady) return;
            
            var price = Securities[PairOtherMain].Price;
            var isBullish = rqrOtherMain[0].Value > rqrOtherMain[1].Value;
            var isBearish = rqrOtherMain[0].Value < rqrOtherMain[1].Value;
            if (isBullish && !wasBullish && !Portfolio.Invested)
            {
                var maxMain = Portfolio.CashBook[MAIN].Amount - MAIN_RESERVE;
                var other = maxMain / price;
                if (other > 0)
                {
                    Log($"{Time}   Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5}");
                    Buy(PairOtherMain, other);
                }
            } else if (isBearish && !wasBearish && Portfolio.Invested) {
                var maxOther = Portfolio.CashBook[OTHER].Amount - OTHER_RESERVE;
                if (maxOther > 0)
                {
                    Log($"{Time}   Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5}");
                    Sell(PairOtherMain, maxOther);
                }
            }
            wasBullish = isBullish;
            wasBearish = isBearish;
        }

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice data)
        {
            if (IsWarmingUp || consolidate) return;

            var price = cryptoOtherMain.Price;// Securities[PAIR].Price;
            var isBullish = rqrOtherMain[0].Value > rqrOtherMain[1].Value;
            var isBearish = rqrOtherMain[0].Value < rqrOtherMain[1].Value;
            var canBuy = Portfolio.CashBook[MAIN].Amount > MAIN_RESERVE;
            var canSell = Portfolio.CashBook[OTHER].Amount > OTHER_RESERVE;
            if (isBullish && !wasBullish && canBuy)
            {
                var maxMain = Portfolio.CashBook[MAIN].Amount - MAIN_RESERVE;
                var other = maxMain / price;
                if (other > 0)
                {
                    Log($"{Time}   Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5}");
                    Buy(PairOtherMain, other);
                }
            } else if (isBearish && !wasBearish && canSell) {
                var maxOther = Portfolio.CashBook[OTHER].Amount - OTHER_RESERVE;
                if (maxOther > 0)
                {
                    Log($"{Time}   Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5}");
                    Sell(PairOtherMain, maxOther);
                }
            }
            wasBullish = isBullish;
            wasBearish = isBearish;
        }

        public override void OnEndOfDay(Symbol symbol)
        {
            base.OnEndOfDay(symbol);
            /*var security = Securities[symbol.Value];
            var lastData = (TradeBar)security?.GetLastData();
            if (lastData != null)
                Plot("Price", symbol.Value, lastData);*/
            Log($"{Time}   Portfolio {Portfolio.CashBook[OTHER].Amount:F5}");
        }

        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();
            Log($"{Time} - TotalPortfolioValue: {Portfolio.TotalPortfolioValue}");
            Log($"{Time} - CashBook: \r\n{Portfolio.CashBook}");
        }

        
    }
}
