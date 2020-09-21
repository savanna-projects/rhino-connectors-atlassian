/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

namespace Rhino.Connectors.XrayCloud.Extensions
{
    internal static class JiraClientExtensions
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
    }
}