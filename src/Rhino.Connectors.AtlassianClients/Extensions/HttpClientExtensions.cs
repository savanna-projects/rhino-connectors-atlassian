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
    /// <summary>
    /// Extension methods for working with HttpClient and HttpHeaders.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Adds the specified header to the collection if it does not already exist.
        /// </summary>
        /// <param name="headers">The HttpRequestHeaders collection.</param>
        /// <param name="name">The name of the header to add.</param>
        /// <param name="value">The value of the header to add.</param>
        public static void AddIfNotExists(this HttpRequestHeaders headers, string name, string value)
        {
            const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

            // Check if the header already exists with the specified value
            var exists = headers.Any(i => i.Key.Equals(name, Compare) && i.Value.Contains(value));

            // If the header already exists, do nothing
            if (exists)
            {
                return;
            }

            // Add the header without validation
            headers.TryAddWithoutValidation(name, value);
        }
    }
}
