
#region imports
using System;
using QuantConnect.Indicators;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities;

#endregion
namespace QuantConnect.Algorithm.CSharp
{
    public class RationalQuadrationRegressionAlgorithm : QCAlgorithm
    {
        private Symbol symbol;
        private RationalQuadraticRegression RQRFast;
        private RationalQuadraticRegression RQRSlow;
        private AverageTrueRange ATRFast;

        public override void Initialize()
        {
            SetStartDate(2024, 11, 27);
            SetEndDate(2024, 12, 02);
            SetCash(100000);

            symbol = AddCrypto("PDAIPLS", Resolution.Minute).Symbol;

            // Initialize Fast and Slow RQR indicators
            RQRFast = new RationalQuadraticRegression(5, 10);  // Fast RQR for immediate trend detection
            RQRSlow = new RationalQuadraticRegression(15, 10); // Slow RQR for broader trend confirmation
            RegisterIndicator(symbol, RQRFast, TimeSpan.FromMinutes(1));
            RegisterIndicator(symbol, RQRSlow, TimeSpan.FromMinutes(5));

            // Initialize ATR for volatility-based position sizing
            ATRFast = ATR(symbol, 14, MovingAverageType.Simple, Resolution.Minute);
        }

        public override void OnData(Slice data)
        {
            if (!RQRFast.IsReady || !RQRSlow.IsReady || !ATRFast.IsReady)
                return;

            // Get the current values of RQR and ATR
            decimal fastRQRValue = RQRFast.Current.Value;
            decimal slowRQRValue = RQRSlow.Current.Value;
            decimal atrValue = ATRFast.Current.Value;

            // Determine RQR Trend Direction
            bool isFastRising = RQRFast.IsBullish(0.01m);  // Fast RQR trend change detection
            bool isFastFalling = RQRFast.IsBearish(0.01m);
            bool isSlowRising = RQRSlow.IsBullish(0.005m); // Slow RQR for stable trend confirmation
            bool isSlowFalling = RQRSlow.IsBearish(0.005m);

            // Dynamic Position Sizing Based on Volatility (ATR)
            decimal maxPositionSize = 1.0m;
            decimal volatilityAdjustment = Math.Min(1.0m, 1.0m / atrValue); // Adjust position size inversely to ATR
            decimal positionSize = maxPositionSize * volatilityAdjustment;

            // Define Buy and Sell Signals
            bool buySignal = isFastRising || isSlowRising; // Buy if fast or slow RQR shows upward trend
            bool sellSignal = isFastFalling || isSlowFalling; // Sell if fast or slow RQR shows downward trend

            // Check current holdings
            var holdings = Portfolio[symbol].Quantity;

            // Execute Trades Based on Signals
            if (buySignal && holdings <= 0)
            {
                SetHoldings(symbol, positionSize);
                Debug($"Buying {symbol} at {Time}, RQRFast: {fastRQRValue}, RQRSlow: {slowRQRValue}, ATR: {atrValue}");
            }
            else if (sellSignal && holdings >= 0)
            {
                Liquidate(symbol);
                Debug($"Selling {symbol} at {Time}, RQRFast: {fastRQRValue}, RQRSlow: {slowRQRValue}, ATR: {atrValue}");
            }
        }
    }
}
