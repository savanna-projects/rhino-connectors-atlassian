/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class RhinoTestRunExtensions
    {
        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestRun.Context.
        /// </summary>
        /// <param name="testRun">RhinoTestRun to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestRun testRun)
        {
            // exit conditions
            if (!testRun.Context.ContainsKey(ContextEntry.Configuration))
            {
                return new JiraAuthentication();
            }

            // get
            var configuration = testRun.Context[ContextEntry.Configuration] as RhinoConfiguration;

            // exit conditions
            if (configuration == default)
            {
                return new JiraAuthentication();
            }

            // get
            return new JiraAuthentication
            {
                AsOsUser = configuration.ProviderConfiguration.AsOsUser,
                Capabilities = configuration.ProviderConfiguration.Capabilities,
                Collection = configuration.ProviderConfiguration.Collection,
                Password = configuration.ProviderConfiguration.Password,
                User = configuration.ProviderConfiguration.User,
                Project = configuration.ProviderConfiguration.Project
            };
        }
    }
}