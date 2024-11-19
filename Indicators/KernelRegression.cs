#region imports
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using System.Drawing;
    using QuantConnect;
    using QuantConnect.Algorithm.Framework;
    using QuantConnect.Algorithm.Framework.Alphas;
    using QuantConnect.Algorithm.Framework.Portfolio;
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
    using QuantConnect.Securities.Positions;
    using QuantConnect.Securities.Forex;
    using QuantConnect.Securities.Crypto;
    using QuantConnect.Securities.Interfaces;
    using QuantConnect.Storage;
#endregion
using TheGainTheory;


namespace QuantConnect.Indicators
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class KernelRegressionBase : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {

        protected decimal[] weights;

        /// <summary>
        /// Initializes a new instance of the KernelRegressionBase class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the Lookback</param>
        protected KernelRegressionBase(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // our first data point just return identity
            if (window.Size == 1)
            {
                return input.Value;
            }
            
            decimal currentWeight = 0.0m;
            decimal cumulativeWeight = 0.0m;
            var maxPrev = Math.Min(weights.Length, window.Count - 1);
            for (int k = 0; k <= maxPrev; k++)
            {
                var y = window[k];
                var w = weights[k];
                currentWeight += y * w;
                cumulativeWeight += w;
            }
    
            var result = cumulativeWeight != 0.0m ? currentWeight / cumulativeWeight : 0.0m;

            return result;
        }

        public static bool IsSignificantPriceChange(decimal diff, decimal previousValue, decimal theta)
        {
            return Math.Abs(diff) / previousValue > theta;
        }

        public bool IsBullish(decimal theta = 0m)
        {
            var diff = Current.Value - Previous.Value;
            return diff > 0 && (theta == 0 || IsSignificantPriceChange(diff, Previous.Value, theta));
        }

        public bool IsBearish(decimal theta = 0m)
        {
            var diff = Current.Value - Previous.Value;
            return diff < 0 && (theta == 0 || IsSignificantPriceChange(diff, Previous.Value, theta));
        }

        public bool WasBullish(int index, decimal theta = 0m)
        {
            var diff = this[index].Value - this[index + 1].Value;
            return diff > 0 && (theta == 0 || IsSignificantPriceChange(diff, this[index + 1].Value, theta));
        }

        public bool WasBearish(int index, decimal theta = 0m)
        {
            var diff = this[index].Value - this[index + 1].Value;
            return diff < 0 && (theta == 0 || IsSignificantPriceChange(diff, this[index + 1].Value, theta));
        }
    }

    public class RationalQuadraticRegression : KernelRegressionBase
    {
        /// <summary>
        /// Initializes a new instance of the RationalQuadraticRegression class with the default name and period
        /// </summary>
        /// <param name="period">The period of the kernel regression</param>
        public RationalQuadraticRegression(int period, int lookback, double relativeWeight = 1.0)
            : this($"RQR({period}, {lookback})", period, lookback, relativeWeight)
        {
            
        }

        /// <summary>
        /// Initializes a new instance of the RationalQuadraticRegression class with the default name and period
        /// </summary>
        /// <param name="period">The period of the kernel regression</param>
        public RationalQuadraticRegression(string name, int period, int lookback, double relativeWeight = 1.0)
            : base(name, period)
        {
            this.weights = KernelFunctions.RationalQuadraticWeights(Math.Max(lookback, period), period, relativeWeight);
        }
    }

    public class GaussianRegression : KernelRegressionBase
    {
        /// <summary>
        /// Initializes a new instance of the RationalQuadraticRegression class with the default name and period
        /// </summary>
        /// <param name="period">The period of the kernel regression</param>
        public GaussianRegression(int period, int lookback)
            : base($"GR({period}, {lookback})", period)
        {
            this.weights = KernelFunctions.GaussianWeights(Math.Max(lookback, period), period);
        }
    }

    public class PeriodicRegression : KernelRegressionBase
    {
        /// <summary>
        /// Initializes a new instance of the RationalQuadraticRegression class with the default name and period
        /// </summary>
        /// <param name="period">The period of the kernel regression</param>
        public PeriodicRegression(int period, int lookback, int periodicPeriod)
            : base($"PR({period}, {lookback})", period)
        {
            this.weights = KernelFunctions.PeriodicWeights(Math.Max(lookback, period), period, periodicPeriod);
        }
    }

    public class LocallyPeriodicRegression : KernelRegressionBase
    {
        /// <summary>
        /// Initializes a new instance of the RationalQuadraticRegression class with the default name and period
        /// </summary>
        /// <param name="period">The period of the kernel regression</param>
        public LocallyPeriodicRegression(int period, int lookback, int periodicPeriod)
            : base($"LPR({period}, {lookback})", period)
        {
            this.weights = KernelFunctions.LocallyPeriodicWeights(Math.Max(lookback, period), period, periodicPeriod);
        }
    }
}