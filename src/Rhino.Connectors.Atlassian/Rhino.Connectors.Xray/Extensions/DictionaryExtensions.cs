/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A collection of <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>XRay style table.</returns>
        public static string ToXrayMarkdown(this IEnumerable<IDictionary<string, object>> data)
        {
            // exit conditions
            if (!data.Any())
            {
                return string.Empty;
            }

            // get columns
            var columns = data.First().Select(i => i.Key);

            // exit conditions
            if (!columns.Any())
            {
                return string.Empty;
            }

            // build header
            var markdown = "||" + string.Join("||", columns) + "||\\r\\n";

            // build rows
            foreach (var dataRow in data)
            {
                markdown += $"|{string.Join("|", dataRow.Select(i => $"{i.Value}"))}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }

        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>XRay style table.</returns>
        public static string ToXrayMarkdown(this IDictionary<string, object> data)
        {
            // exit conditions
            if (data.Keys.Count == 0)
            {
                return string.Empty;
            }

            // build header
            var markdown = "||Key||Value||\\r\\n";

            // append rows
            foreach (var item in data)
            {
                markdown += $"|{item.Key}|{item.Value}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }
    }
}
