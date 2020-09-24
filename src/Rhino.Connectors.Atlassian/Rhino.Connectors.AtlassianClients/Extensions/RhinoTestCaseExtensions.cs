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
    public static class RhinoTestCaseExtensions
    {
        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestCase.Context.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestCase testCase)
        {
            // exit conditions
            if (!testCase.Context.ContainsKey(ContextEntry.Configuration))
            {
                return new JiraAuthentication();
            }

            // get
            var configuration = testCase.Context[ContextEntry.Configuration] as RhinoConfiguration;

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