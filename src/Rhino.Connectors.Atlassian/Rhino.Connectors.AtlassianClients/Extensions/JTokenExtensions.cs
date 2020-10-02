/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json.Linq;

using System;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JTokenExtensions
    {
        /// <summary>
        /// Converts JToken into JObject.
        /// </summary>
        /// <param name="token">JToken to convert.</param>
        /// <returns>JObject instance.</returns>
        public static JObject AsJObject(this JToken token)
        {
            return JObject.Parse($"{token}");
        }

        /// <summary>
        /// Gets a string as JToken or empty JToken if not possible
        /// </summary>
        /// <param name="token"><see cref="string"/> to parse.</param>
        /// <returns>JObject based on the input string or empty if not possible.</returns>
        public static JObject AsJObject(this string token)
        {
            // setup
            var onToken = DoAsJToken(token);

            // get
            return JObject.Parse($"{onToken}");
        }

        /// <summary>
        /// Gets a string as JToken or empty JToken if not possible
        /// </summary>
        /// <param name="token"><see cref="string"/> to parse.</param>
        /// <returns>JToken based on the input string or empty if not possible.</returns>
        public static JToken AsJToken(this string token)
        {
            return DoAsJToken(token);
        }

        private static JToken DoAsJToken(string token)
        {
            // exit conditions
            if (string.IsNullOrEmpty(token))
            {
                return JToken.Parse("{}");
            }

            // parse
            try
            {
                return JToken.Parse(token);
            }
            catch (Exception e) when (e != null)
            {
                return JToken.Parse("{}");
            }
        }
    }
}