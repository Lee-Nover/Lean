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
        private Crypto crypto;
        private Symbol symbol;
        private RationalQuadraticRegression rqr;
        private bool wasBullish;
        private bool wasBearish;

        const string MAIN = "WPLS";
        const string OTHER = "PDAI";
        const string PAIR = OTHER + MAIN;
        const decimal MAIN_RESERVE = 100000m;
        const decimal OTHER_RESERVE = 0m;
        public override void Initialize()
        {
            // Locally Lean installs free sample data, to download more data please visit https://www.quantconnect.com/docs/v2/lean-cli/datasets/downloading-data
            // 20180405_trade
            
            SetStartDate(2024, 02, 02);
            SetEndDate(2024, 03, 14);
            
            SetBrokerageModel(BrokerageName.Default, AccountType.Cash);
            
            SetAccountCurrency(MAIN);
            Market.Add("pulsechain", 369);
            crypto = AddCrypto(PAIR, Resolution.Hour, "pulsechain");
            symbol = crypto.Symbol;
            SetCash(MAIN, 2000000m);
            SetCash(OTHER, 100000m);
            
            rqr = new RationalQuadraticRegression(10, 10, 1);
            string name = CreateIndicatorName(symbol, rqr.Name, null);
            RegisterIndicator(symbol, rqr, null);
            SetWarmUp(10);
            
            /*var chart = new Chart("Price");
            AddChart(chart);
            chart.AddSeries(new Series(rqr.Name, SeriesType.Line, "$", Color.Orange));
            chart.AddSeries(new CandlestickSeries(OTHER, OTHER));*/

            //Consolidate(symbol, TimeSpan.FromMinutes(5), OnDataConsolidated);
        }

        private void OnDataConsolidated(TradeBar bar)
        {
            return;
            rqr.Update(Time, bar.Close);
            if (!rqr.IsReady) return;
            
            var price = Securities[PAIR].Price;
            var isBullish = rqr[0].Value > rqr[1].Value;
            var isBearish = rqr[0].Value < rqr[1].Value;
            if (isBullish && !wasBullish && !Portfolio.Invested)
            {
                var maxMain = Portfolio.CashBook[MAIN].Amount - MAIN_RESERVE;
                var other = maxMain / price;
                if (other > 0)
                {
                    Log($"{Time}   Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5}");
                    Buy(PAIR, other);
                }
            } else if (isBearish && !wasBearish && Portfolio.Invested) {
                var maxOther = Portfolio.CashBook[OTHER].Amount - OTHER_RESERVE;
                if (maxOther > 0)
                {
                    Log($"{Time}   Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5}");
                    Sell(PAIR, maxOther);
                }
            }
            wasBullish = isBullish;
            wasBearish = isBearish;
        }

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice data)
        {
            if (IsWarmingUp) return;
            var price = crypto.Price;// Securities[PAIR].Price;
            var isBullish = rqr[0].Value > rqr[1].Value;
            var isBearish = rqr[0].Value < rqr[1].Value;
            var canBuy = Portfolio.CashBook[MAIN].Amount > MAIN_RESERVE;
            var canSell = Portfolio.CashBook[OTHER].Amount > OTHER_RESERVE;
            if (isBullish && !wasBullish && canBuy)
            {
                var maxMain = Portfolio.CashBook[MAIN].Amount - MAIN_RESERVE;
                var other = maxMain / price;
                if (other > 0)
                {
                    Log($"{Time}   Buying {other:F5} {OTHER} for {MAIN} {maxMain:F5}");
                    Buy(PAIR, other);
                }
            } else if (isBearish && !wasBearish && canSell) {
                var maxOther = Portfolio.CashBook[OTHER].Amount - OTHER_RESERVE;
                if (maxOther > 0)
                {
                    Log($"{Time}   Selling {maxOther:F5} {OTHER} for {MAIN} {maxOther * price:F5}");
                    Sell(PAIR, maxOther);
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
