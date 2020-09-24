/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class MarkdownExtensions
    {
        /// <summary>
        /// Parse a collection of key/value pairs into a Jira compliant markdown table.
        /// </summary>
        /// <param name="data">Collection to parse.</param>
        /// <returns>Jira compliant markdown table.</returns>
        public static string ToMarkdown(this IEnumerable<IDictionary<string, object>> data)
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
    }
}