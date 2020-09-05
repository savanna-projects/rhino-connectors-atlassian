/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class CollectionExtensions
    {
        /// <summary>
        /// Adds the elements of the specified collection to the end of the <see cref="ConcurrentBag{T}"/>.
        /// </summary>
        /// <param name="bag">This <see cref="ConcurrentBag{T}"/> instance.</param>
        /// <param name="range">The collection whose elements should be added to the end of the <see cref="ConcurrentBag{T}"/>.</param>
        public static void AddRange<T>(this ConcurrentBag<T> bag, IEnumerable<T> range)
        {
            foreach (var item in range)
            {
                bag.Add(item);
            }
        }
    }
}