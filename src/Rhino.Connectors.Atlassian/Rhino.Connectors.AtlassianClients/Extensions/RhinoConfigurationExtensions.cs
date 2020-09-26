/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

using System.Collections.Generic;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class RhinoConfigurationExtensions
    {
        /// <summary>
        /// Gets a bucket size from RhinoConfiguration capabilities or default value if not exists.
        /// </summary>
        /// <param name="configuration">Configuration to get bucket size from.</param>
        /// <returns>Bucket size.</returns>
        public static int GetBuketSize(this RhinoConfiguration configuration)
        {
            // setup
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // get bucket size value
            if (capabilities?.ContainsKey(ProviderCapability.BucketSize) == false)
            {
                return 15;
            }
            int.TryParse($"{capabilities[ProviderCapability.BucketSize]}", out int bucketSizeOut);

            // return final value
            return bucketSizeOut == 0 ? 15 : bucketSizeOut;
        }

        #region *** Put Issue Types ***
        /// <summary>
        /// Apply issue types into RhinoConfiguration capabilities or default types if needed.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to apply issue types to</param>
        public static void PutIssueTypes(this RhinoConfiguration configuration)
        {
            // setup
            var defaultMap = DefaultTypesMap();
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // factor
            foreach (var key in defaultMap.Keys)
            {
                if (capabilities.ContainsKey(key))
                {
                    continue;
                }
                capabilities[key] = defaultMap[key];
            }
        }

        private static IDictionary<string, string> DefaultTypesMap() => new Dictionary<string, string>
        {
            [AtlassianCapabilities.TestType] = "Test",
            [AtlassianCapabilities.SetType] = "Test Set",
            [AtlassianCapabilities.PlanType] = "Test Plan",
            [AtlassianCapabilities.PreconditionsType] = "Pre-condition",
            [AtlassianCapabilities.ExecutionType] = "Test Execution",
            [AtlassianCapabilities.BugType] = "Bug"
        };
        #endregion

        /// <summary>
        /// Gets a value indicates if this is a dry run (will not create test execution report)
        /// </summary>
        /// <param name="configuration">RhinoConfiguration from which to get capabilities.</param>
        /// <returns><see cref="true"/> for id dryRun; <see cref="false"/> if not.</returns>
        public static bool IsDryRun(this RhinoConfiguration configuration)
        {
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // setup conditions
            var isKey = capabilities.ContainsKey(AtlassianCapabilities.DryRun);
            var value = isKey ? $"{capabilities[AtlassianCapabilities.DryRun]}" : "false";

            // evaluate
            bool.TryParse(value, out bool dryRunOut);
            return isKey && dryRunOut;
        }
    }
}