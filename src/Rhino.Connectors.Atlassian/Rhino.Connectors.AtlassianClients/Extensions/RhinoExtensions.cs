/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class RhinoExtensions
    {
        #region *** Authentication  ***
        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestCase.Context.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestCase testCase)
        {
            return DoGetAuthentication(testCase.Context);
        }

        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestRun.Context.
        /// </summary>
        /// <param name="testRun">RhinoTestRun to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestRun testRun)
        {
            return DoGetAuthentication(testRun.Context);
        }

        private static JiraAuthentication DoGetAuthentication(IDictionary<string, object> context)
        {
            // exit conditions
            if (!context.ContainsKey(ContextEntry.Configuration))
            {
                return new JiraAuthentication();
            }

            // get
            var configuration = context[ContextEntry.Configuration] as RhinoConfiguration;

            // exit conditions
            if (configuration == default)
            {
                return new JiraAuthentication();
            }

            // get
            return new JiraAuthentication
            {
                AsOsUser = configuration.ConnectorConfiguration.AsOsUser,
                Capabilities = configuration.Capabilities,
                Collection = configuration.ConnectorConfiguration.Collection,
                Password = configuration.ConnectorConfiguration.Password,
                User = configuration.ConnectorConfiguration.User,
                Project = configuration.ConnectorConfiguration.Project
            };
        }
        #endregion

        /// <summary>
        /// Gets a comment text for failed test case which includes meta data information.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to get comment for.</param>
        /// <returns>Jira markdown fail comment.</returns>
        public static string GetFailComment(this RhinoTestCase testCase)
        {
            // setup
            var failedSteps = testCase.Steps.Where(i => !i.Actual).Select(i => ((JToken)i.Context["testStep"])["index"]);

            // exit conditions
            if (!failedSteps.Any())
            {
                return string.Empty;
            }

            // setup
            var environment = testCase.MarkdownEnvironment();
            var platform = testCase.MarkdownPlatform();
            var dataSource = testCase.MarkdownDataSource();

            // build
            var header =
                "----\r\n" +
                $"*{DateTime.UtcNow} UTC* \r\n" +
                $"*Failed On Iteration:* {testCase.Iteration}\r\n" +
                $"*On Steps:* {string.Join(", ", failedSteps)}\r\n" +
                $"*On Application:* {environment}\r\n";

            var body = (platform + dataSource)
                .Replace("\\r\\n", "\n")
                .Replace(@"\""", "\"")
                .Replace("----\r\n", string.Empty);

            // return
            return header + body;
        }

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