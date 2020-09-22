/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Net.Http.Headers;
using System.Text;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JiraClientExtensions
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
                AsOsUser = configuration.ProviderConfiguration.AsOsUser,
                Collection = configuration.ProviderConfiguration.Collection,
                Password = configuration.ProviderConfiguration.Password,
                User = configuration.ProviderConfiguration.User,
                Project = configuration.ProviderConfiguration.Project
            };
        }

        /// <summary>
        /// Gets a <see cref="AuthenticationHeaderValue"/> object.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to create <see cref="AuthenticationHeaderValue"/> by.</param>
        /// <returns><see cref="AuthenticationHeaderValue"/> object</returns>
        public static AuthenticationHeaderValue GetAuthenticationHeader(this RhinoConfiguration configuration)
        {
            return DoGetAuthenticationHeader(configuration.ProviderConfiguration.User, configuration.ProviderConfiguration.Password);
        }

        /// <summary>
        /// Gets a <see cref="AuthenticationHeaderValue"/> object.
        /// </summary>
        /// <param name="authentication">RhinoConfigJiraAuthenticationuration to create <see cref="AuthenticationHeaderValue"/> by.</param>
        /// <returns><see cref="AuthenticationHeaderValue"/> object</returns>
        public static AuthenticationHeaderValue GetAuthenticationHeader(this JiraAuthentication authentication)
        {
            return DoGetAuthenticationHeader(authentication.User, authentication.Password);
        }

        private static AuthenticationHeaderValue DoGetAuthenticationHeader(string user, string password)
        {
            // setup: provider authentication and base address
            var header = $"{user}:{password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

            // header
            return new AuthenticationHeaderValue("Basic", encodedHeader);
        }
    }
}
