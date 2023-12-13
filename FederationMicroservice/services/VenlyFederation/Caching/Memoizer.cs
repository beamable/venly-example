using System;
using System.Collections.Concurrent;

namespace Beamable.VenlyFederation.Caching
{
    public static class Memoizer
    {
        public static Func<TInput, TResult> Memoize<TInput, TResult>(this Func<TInput, TResult> func) where TInput : notnull
        {
            var memo = new ConcurrentDictionary<TInput, TResult>();
            return input => memo.GetOrAdd(input, func);
        }
    }
}