/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using System;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    /// <summary>
    /// Extension method to convert a JToken to a JObject.
    /// </summary>
    public static class JTokenExtensions
    {
        /// <summary>
        /// Converts the provided JToken to a JObject.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>A JObject representation of the JToken.</returns>
        public static JObject ConvertToJObject(this JToken token)
        {
            try
            {
                // Convert JToken to JSON string or use an empty object if JToken is default
                var json = token == default ? "{}" : $"{token}";

                // Parse the JSON string into a JObject
                return JObject.Parse(json);
            }
            catch (Exception)
            {
                // Return an empty JObject in case of any exception during parsing
                return JObject.Parse("{}");
            }
        }

        /// <summary>
        /// Converts the provided string to a JToken.
        /// </summary>
        /// <param name="token">The string to convert.</param>
        /// <returns>A JToken representation of the string.</returns>
        public static JToken ConvertToJToken(this string token)
        {
            // Use an empty object if the provided string is null or empty
            token = string.IsNullOrEmpty(token) ? "{}" : token;

            // Check if the string is a valid JSON format; otherwise, use an empty object
            if (!token.IsJson())
            {
                token = "{}";
            }

            // Parse the string into a JToken
            return JToken.Parse(token);
        }
    }
}
