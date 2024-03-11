using System;
/* KernelFunctions by jdehorty, converted from pinescript to c#.

// This source code is subject to the terms of the Mozilla Public License 2.0 at https://mozilla.org/MPL/2.0/
// © jdehorty
// @version=5

// @description This library provides non-repainting kernel functions for Nadaraya-Watson estimator implementations. This allows for easy substition/comparison of different kernel functions for one another in indicators. Furthermore, kernels can easily be combined with other kernels to create newer, more customized kernels.
library("KernelFunctions", true)

// @function Rational Quadratic Kernel - An infinite sum of Gaussian Kernels of different length scales.
// @param _src <float series> The source series.
// @param _lookback <simple int> The number of bars used for the estimation. This is a sliding value that represents the most recent historical bars.
// @param _relativeWeight <simple float> Relative weighting of time frames. Smaller values resut in a more stretched out curve and larger values will result in a more wiggly curve. As this value approaches zero, the longer time frames will exert more influence on the estimation. As this value approaches infinity, the behavior of the Rational Quadratic Kernel will become identical to the Gaussian kernel.
// @param _startAtBar <simple int> Bar index on which to start regression. The first bars of a chart are often highly volatile, and omission of these initial bars often leads to a better overall fit.
// @returns yhat <float series> The estimated values according to the Rational Quadratic Kernel.
export rationalQuadratic(series float _src, simple int _lookback, simple float _relativeWeight, simple int startAtBar) =>
	float _currentWeight = 0.
	float _cumulativeWeight = 0.
	_size = array.size(array.from(_src))
    for i = 0 to _size + startAtBar
        y = _src[i]
        w = math.pow(1 + (math.pow(i, 2) / ((math.pow(_lookback, 2) * 2 * _relativeWeight))), -_relativeWeight)
        _currentWeight += y*w
        _cumulativeWeight += w
    yhat = _currentWeight / _cumulativeWeight
    yhat

// @function Gaussian Kernel - A weighted average of the source series. The weights are determined by the Radial Basis Function (RBF).
// @param _src <float series> The source series.
// @param _lookback <simple int> The number of bars used for the estimation. This is a sliding value that represents the most recent historical bars.
// @param _startAtBar <simple int> Bar index on which to start regression. The first bars of a chart are often highly volatile, and omission of these initial bars often leads to a better overall fit.
// @returns yhat <float series> The estimated values according to the Gaussian Kernel.
export gaussian(series float _src, simple int _lookback, simple int startAtBar) =>
    float _currentWeight = 0.
    float _cumulativeWeight = 0.
    _size = array.size(array.from(_src))
    for i = 0 to _size + startAtBar
        y = _src[i] 
        w = math.exp(-math.pow(i, 2) / (2 * math.pow(_lookback, 2)))
        _currentWeight += y*w
        _cumulativeWeight += w
    yhat = _currentWeight / _cumulativeWeight
    yhat

// @function Periodic Kernel - The periodic kernel (derived by David Mackay) allows one to model functions which repeat themselves exactly.
// @param _src <float series> The source series.
// @param _lookback <simple int> The number of bars used for the estimation. This is a sliding value that represents the most recent historical bars.
// @param _period <simple int> The distance between repititions of the function.
// @param _startAtBar <simple int> Bar index on which to start regression. The first bars of a chart are often highly volatile, and omission of these initial bars often leads to a better overall fit.
// @returns yhat <float series> The estimated values according to the Periodic Kernel.
export periodic(series float _src, simple int _lookback, simple int _period, simple int startAtBar) =>
    float _currentWeight = 0.
    float _cumulativeWeight = 0.
    _size = array.size(array.from(_src))
    for i = 0 to _size + startAtBar
        y = _src[i]
        w = math.exp(-2*math.pow(math.sin(math.pi * i / _period), 2) / math.pow(_lookback, 2))
        _currentWeight += y*w
        _cumulativeWeight += w
    yhat = _currentWeight / _cumulativeWeight
    yhat

// @function Locally Periodic Kernel - The locally periodic kernel is a periodic function that slowly varies with time. It is the product of the Periodic Kernel and the Gaussian Kernel.
// @param _src <float series> The source series.
// @param _lookback <simple int> The number of bars used for the estimation. This is a sliding value that represents the most recent historical bars.
// @param _period <simple int> The distance between repititions of the function.
// @param _startAtBar <simple int> Bar index on which to start regression. The first bars of a chart are often highly volatile, and omission of these initial bars often leads to a better overall fit.
// @returns yhat <float series> The estimated values according to the Locally Periodic Kernel.
export locallyPeriodic(series float _src, simple int _lookback, simple int _period, simple int startAtBar) =>
    float _currentWeight = 0.
    float _cumulativeWeight = 0.
    _size = array.size(array.from(_src))
    for i = 0 to _size + startAtBar
        y = _src[i]
        w = math.exp(-2*math.pow(math.sin(math.pi * i / _period), 2) / math.pow(_lookback, 2)) * math.exp(-math.pow(i, 2) / (2 * math.pow(_lookback, 2)))
        _currentWeight += y*w
        _cumulativeWeight += w
    yhat = _currentWeight / _cumulativeWeight
    yhat

*/

namespace TheGainTheory
{
    public class KernelFunctions
    {
        public static decimal[] RationalQuadraticWeights(int count, int lookback, double relativeWeight)
        {
            var weights = new decimal[count];
            var lbWeighted = lookback * lookback * 2 * relativeWeight;
            for (int i = 0; i < count; i++)
                weights[i] = (decimal)Math.Pow(1 + (i * i / lbWeighted), -relativeWeight);
            return weights;
        }

        public static decimal[] GaussianWeights(int count, int lookback)
        {
            decimal[] weights = new decimal[count];
            var lbSqX2 = 2 * lookback * lookback;
            for (int i = 0; i < count; i++)
            {
                var distanceSquared = i * i;
                weights[i] = (decimal)Math.Exp(-distanceSquared / lbSqX2);
            }
            return weights;
        }
        
        public static decimal[] PeriodicWeights(int count, int lookback, int period)
        {
            decimal[] weights = new decimal[count];
            var lbSq = lookback * lookback;
            for (int i = 0; i < count; i++)
            {
                var sinValue = Math.Sin(Math.PI * i / period);
                weights[i] = (decimal)Math.Exp(-2 * sinValue * sinValue / lbSq);
            }
            return weights;
        }
        
        public static decimal[] LocallyPeriodicWeights(int count, int lookback, int period)
        {
            decimal[] weights = new decimal[count];
            var lbSq = lookback * lookback;
            var lbSqX2 = lbSq * 2;
            for (int i = 0; i < count; i++)
            {
                var sinValue = Math.Sin(Math.PI * i / period);
                var distanceSquared = i * i;
                weights[i] = (decimal)(Math.Exp(-2 * sinValue * sinValue / lbSq) *
                                       Math.Exp(-distanceSquared / lbSqX2));
            }
            return weights;
        }
        
        public static decimal[] ApplyWeights(decimal[] src, decimal[] weights, int count)
        {
            int size = src.Length;
            int lookback = weights.Length;
            if (count < 1 || count > size)
                count = size;
            
            decimal[] result = new decimal[count];
            for (int i = size - count, idxResult = 0; i < size; i++, idxResult++)
            {
                decimal currentWeight = 0.0m;
                decimal cumulativeWeight = 0.0m;
                var maxPrev = Math.Min(lookback, i);
                for (int k = 0; k <= maxPrev; k++)
                {
                    var y = src[i-k];
                    var w = weights[k];
                    currentWeight += y * w;
                    cumulativeWeight += w;
                }
        
                result[idxResult] = cumulativeWeight != 0.0m ? currentWeight / cumulativeWeight : 0.0m;
            }
        
            return result;
        }
        
        public static decimal[] RationalQuadratic(decimal[] src, int lookback, double relativeWeight, int prevBars, int count)
        {
            var weights = RationalQuadraticWeights(prevBars, lookback, relativeWeight);
            return ApplyWeights(src, weights, count);
        }

        public static decimal[] Gaussian(decimal[] src, int lookback, int prevBars, int count)
        {
            var weights = GaussianWeights(prevBars, lookback);
            return ApplyWeights(src, weights, count);
        }
        
        public static decimal[] Periodic(decimal[] src, int lookback, int period, int prevBars, int count)
        {
            var weights = PeriodicWeights(prevBars, lookback, period);
            return ApplyWeights(src, weights, count);
        }
        
        public static decimal[] LocallyPeriodic(decimal[] src, int lookback, int period, int prevBars, int count)
        {
            var weights = LocallyPeriodicWeights(prevBars, lookback, period);
            return ApplyWeights(src, weights, count);
        }
        
    }    
}
