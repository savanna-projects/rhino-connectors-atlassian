/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JiraAuthenticationExtensions
    {
        /// <summary>
        /// Gets a JiraAuthentication object.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to create JiraAuthentication by.</param>
        /// <returns>JiraAuthentication object</returns>
        public static JiraAuthentication GetJiraAuthentication(this RhinoConfiguration configuration)
        {
            return new JiraAuthentication
            {
                AsOsUser = configuration.ConnectorConfiguration.AsOsUser,
                Collection = configuration.ConnectorConfiguration.Collection,
                Password = configuration.ConnectorConfiguration.Password,
                UserName = configuration.ConnectorConfiguration.UserName,
                Project = configuration.ConnectorConfiguration.Project,
                Capabilities = configuration.Capabilities
            };
        }

        #region *** Get Capabilities      ***
        /// <summary>
        /// Gets a capability from JiraAuthentication capabilities dictionary.
        /// </summary>
        /// <typeparam name="T">The value type to be returned from the capabilities dictionary.</typeparam>
        /// <param name="authentication">JiraAuthentication instance from which to get the capability.</param>
        /// <param name="capability">The capability name.</param>
        /// <param name="defaultValue">Default value to be returned if the capability cannot be found.</param>
        /// <returns>The capability value of the provided type or default value.</returns>
        public static T GetCapability<T>(this JiraAuthentication authentication, string capability, T defaultValue)
        {
            // setup
            var capabilites = authentication?.Capabilities != default
                ? authentication.Capabilities
                : new Dictionary<string, object>();
            var path = $"..{capability}";

            // get
            return DoGetCapability(capabilites, path, defaultValue);
        }

        /// <summary>
        /// Gets a capability from JiraAuthentication capabilities dictionary.
        /// </summary>
        /// <typeparam name="T">The value type to be returned from the capabilities dictionary.</typeparam>
        /// <param name="capabilities">A collection of Capabilites.</param>
        /// <param name="capability">The capability name.</param>
        /// <param name="defaultValue">Default value to be returned if the capability cannot be found.</param>
        /// <returns>The capability value of the provided type or default value.</returns>
        public static T GetCapability<T>(this IDictionary<string, object> capabilities, string capability, T defaultValue)
        {
            // setup
            var path = $"..{capability}";

            // get
            return DoGetCapability(capabilities, path, defaultValue);
        }

        /// <summary>
        /// Gets a capability from JiraAuthentication capabilities dictionary.
        /// </summary>
        /// <typeparam name="T">The value type to be returned from the capabilities dictionary.</typeparam>
        /// <param name="capabilities">A collection of Capabilites.</param>
        /// <param name="connector">If specified, the search scope will be the connector options.</param>
        /// <param name="capability">The capability name.</param>
        /// <param name="defaultValue">Default value to be returned if the capability cannot be found.</param>
        /// <returns>The capability value of the provided type or default value.</returns>
        public static T GetCapability<T>(this IDictionary<string, object> capabilities, string connector, string capability, T defaultValue)
        {
            // setup
            var isConnector = !string.IsNullOrEmpty(connector) && capabilities?.ContainsKey($"{connector}:options") == true;
            var path = isConnector ? $"..{connector}:options.{capability}" : $"..{capability}";

            // get
            return DoGetCapability(capabilities, path, defaultValue);
        }

        private static T DoGetCapability<T>(IDictionary<string, object> capabilities, string path, T defaultValue)
        {
            try
            {
                // setup
                var onCapabilities = capabilities != default
                    ? JObject.Parse(JsonConvert.SerializeObject(capabilities))
                    : JObject.Parse("{}");

                // get
                var value = onCapabilities.SelectTokens(path)?.FirstOrDefault();

                // get
                return value == default ? defaultValue : value.ToObject<T>();
            }
            catch (Exception e) when (e != null)
            {
                return defaultValue;
            }
        }
        #endregion

        #region *** Authentication Header ***
        /// <summary>
        /// Gets a <see cref="AuthenticationHeaderValue"/> object.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to create <see cref="AuthenticationHeaderValue"/> by.</param>
        /// <returns><see cref="AuthenticationHeaderValue"/> object</returns>
        public static AuthenticationHeaderValue GetAuthenticationHeader(this RhinoConfiguration configuration)
        {
            return DoGetAuthenticationHeader(configuration.ConnectorConfiguration.UserName, configuration.ConnectorConfiguration.Password);
        }

        /// <summary>
        /// Gets a <see cref="AuthenticationHeaderValue"/> object.
        /// </summary>
        /// <param name="authentication">RhinoConfigJiraAuthenticationuration to create <see cref="AuthenticationHeaderValue"/> by.</param>
        /// <returns><see cref="AuthenticationHeaderValue"/> object</returns>
        public static AuthenticationHeaderValue GetAuthenticationHeader(this JiraAuthentication authentication)
        {
            return DoGetAuthenticationHeader(authentication.UserName, authentication.Password);
        }

        private static AuthenticationHeaderValue DoGetAuthenticationHeader(string user, string password)
        {
            // setup: provider authentication and base address
            var header = $"{user}:{password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

            // header
            return new AuthenticationHeaderValue("Basic", encodedHeader);
        }
        #endregion
    }
}