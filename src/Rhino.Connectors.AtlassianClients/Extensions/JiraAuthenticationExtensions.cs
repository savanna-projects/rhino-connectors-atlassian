/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Net.Http.Headers;
using System.Text;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    /// <summary>
    /// Extension methods for creating a new JiraAuthentication instance from RhinoConfiguration.
    /// </summary>
    public static class JiraAuthenticationExtensions
    {
        #region *** Capabilities   ***
        /// <summary>
        /// Extension method to retrieve a capability from JiraAuthentication properties.
        /// </summary>
        /// <typeparam name="T">The type of the capability.</typeparam>
        /// <param name="authentication">The JiraAuthentication instance.</param>
        /// <param name="capability">The name of the capability to retrieve.</param>
        /// <param name="defaultValue">The default value if the capability is not found.</param>
        /// <returns>The value of the capability or the default value if not found.</returns>
        public static T GetCapability<T>(this JiraAuthentication authentication, string capability, T defaultValue)
        {
            // Delegate the capability retrieval to the Properties extension method
            return authentication.Properties.GetCapability(capability, defaultValue);
        }
        #endregion

        #region *** Authentication ***
        /// <summary>
        /// Extension method to create a new AuthenticationHeaderValue using RhinoConfiguration credentials.
        /// </summary>
        /// <param name="configuration">The RhinoConfiguration instance.</param>
        /// <returns>The AuthenticationHeaderValue instance.</returns>
        public static AuthenticationHeaderValue NewAuthenticationHeader(this RhinoConfiguration configuration)
        {
            // Retrieve username and password from RhinoConfiguration
            var username = configuration.ConnectorConfiguration.Username;
            var password = configuration.ConnectorConfiguration.Password;

            // Delegate to the common NewAuthenticationHeader method
            return NewAuthenticationHeader(username, password);
        }

        /// <summary>
        /// Extension method to create a new AuthenticationHeaderValue using JiraAuthentication credentials.
        /// </summary>
        /// <param name="authentication">The JiraAuthentication instance.</param>
        /// <returns>The AuthenticationHeaderValue instance.</returns>
        public static AuthenticationHeaderValue NewAuthenticationHeader(this JiraAuthentication authentication)
        {
            // Retrieve username and password from JiraAuthentication
            var username = authentication.Username;
            var password = authentication.Password;

            // Delegate to the common NewAuthenticationHeader method
            return NewAuthenticationHeader(username, password);
        }

        // Common method to create a new AuthenticationHeaderValue with the provided credentials.
        private static AuthenticationHeaderValue NewAuthenticationHeader(string user, string password)
        {
            // Combine username and password and encode to Base64
            var header = $"{user}:{password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

            // Create and return AuthenticationHeaderValue
            return new AuthenticationHeaderValue("Basic", encodedHeader);
        }

        /// <summary>
        /// Creates a new JiraAuthentication instance based on RhinoConfiguration.
        /// </summary>
        /// <param name="configuration">The RhinoConfiguration instance.</param>
        /// <returns>A new JiraAuthentication instance.</returns>
        public static JiraAuthentication NewJiraAuthentication(this RhinoConfiguration configuration)
        {
            // Create a new JiraAuthentication instance and populate its properties
            return new JiraAuthentication
            {
                AsOsUser = configuration.ConnectorConfiguration.AsOsUser,
                Collection = configuration.ConnectorConfiguration.Collection,
                Password = configuration.ConnectorConfiguration.Password,
                Username = configuration.ConnectorConfiguration.Username,
                Project = configuration.ConnectorConfiguration.Project,
                Properties = configuration.Capabilities
            };
        }
        #endregion
    }
}