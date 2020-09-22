/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System;
using System.Linq;
using System.Net.Http.Headers;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class HttpExtensions
    {
        /// <summary>
        /// Adds the specified header and its value into the <see cref="HttpHeaders"/> collection.
        /// </summary>
        /// <param name="headers">This <see cref="HttpRequestHeaders"/> instance.</param>
        /// <param name="name">The header to add to the collection.</param>
        /// <param name="value">The content of the header.</param>
        public static void AddIfNotExists(this HttpRequestHeaders headers, string name, string value)
        {
            // constants
            const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

            // find
            var isExists = headers.Any(i => i.Key.Equals(name, Compare) && i.Value.Contains(value));

            // exit conditions
            if (isExists)
            {
                return;
            }
            headers.TryAddWithoutValidation(name, value);
        }
    }
}