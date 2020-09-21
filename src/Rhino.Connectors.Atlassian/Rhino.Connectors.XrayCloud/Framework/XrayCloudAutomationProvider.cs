/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;

using Rhino.Api;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.XrayCloud.Extensions;

using System;
using System.Collections.Generic;

namespace Rhino.Connectors.XrayCloud.Framework
{
    /// <summary>
    /// XRay connector for using XRay tests as Rhino Specs.
    /// </summary>
    public class XrayCloudAutomationProvider : ProviderManager
    {
        // state: global parameters
        private readonly ILogger logger;
        private readonly JiraClient jiraClient;
        private readonly IDictionary<string, object> capabilities;
        private readonly int bucketSize;

        #region *** Constructors      ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this provider.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
            : base(configuration, types, logger)
        {
            // setup
            this.logger = logger?.Setup(loggerName: nameof(XrayCloudAutomationProvider));
            jiraClient = new JiraClient(configuration.GetJiraAuthentication());

            // capabilities
            bucketSize = GetBuketSize(configuration.ProviderConfiguration.Capabilities);
            PutIssueTypes(configuration.ProviderConfiguration.Capabilities);
            capabilities = configuration.ProviderConfiguration.Capabilities;
        }
        #endregion

        // CAPABILITIES
        private int GetBuketSize(IDictionary<string, object> capabilities)
        {
            // get bucket size value
            if (capabilities?.ContainsKey(ProviderCapability.BucketSize) == false)
            {
                return 15;
            }
            int.TryParse($"{capabilities[ProviderCapability.BucketSize]}", out int bucketSizeOut);

            // return final value
            return bucketSizeOut == 0 ? 15 : bucketSizeOut;
        }

        private void PutIssueTypes(IDictionary<string, object> capabilities)
        {
            if (!capabilities.ContainsKey(AtlassianCapabilities.TestType))
            {
                capabilities[AtlassianCapabilities.TestType] = "Test";
            }
            if (!capabilities.ContainsKey(AtlassianCapabilities.SetType))
            {
                capabilities[AtlassianCapabilities.SetType] = "Test Set";
            }
            if (!capabilities.ContainsKey(AtlassianCapabilities.PlanType))
            {
                capabilities[AtlassianCapabilities.PlanType] = "Test Plan";
            }
            if (!capabilities.ContainsKey(AtlassianCapabilities.PreconditionsType))
            {
                capabilities[AtlassianCapabilities.PreconditionsType] = "Pre-Condition";
            }
            if (!capabilities.ContainsKey(AtlassianCapabilities.ExecutionType))
            {
                capabilities[AtlassianCapabilities.ExecutionType] = "Test Execution";
            }
            if (!capabilities.ContainsKey(AtlassianCapabilities.BugType))
            {
                capabilities[AtlassianCapabilities.BugType] = "Bug";
            }
        }

        // UTILITIES
        private void DoUpdateTestResults(RhinoTestCase testCase)
        {
            //try
            //{
            //    // setup
            //    var forUploadOutcomes = new[] { "PASS", "FAIL" };

            //    // exit conditions
            //    var outcome = "TODO";
            //    if (testCase.Context.ContainsKey("outcome"))
            //    {
            //        outcome = $"{testCase.Context["outcome"]}";
            //    }
            //    testCase.SetOutcome();

            //    // attachments
            //    if (forUploadOutcomes.Contains(outcome.ToUpper()))
            //    {
            //        testCase.UploadEvidences();
            //    }

            //    // fail message
            //    if (outcome.Equals("FAIL", Compare) || testCase.Steps.Any(i => i.Exception != default))
            //    {
            //        var comment = testCase.GetFailComment();
            //        testCase.PutResultComment(comment);
            //    }
            //}
            //catch (Exception e) when (e != null)
            //{
            //    logger?.Error($"Failed to update test results for [{testCase.Key}]", e);
            //}
        }

        private bool IsDryRun(IDictionary<string, object> capabilities)
        {
            // setup conditions
            var isKey = capabilities.ContainsKey(AtlassianCapabilities.DryRun);
            var value = isKey ? $"{capabilities[AtlassianCapabilities.DryRun]}" : "false";

            // evaluate
            bool.TryParse(value, out bool dryRunOut);
            return isKey && dryRunOut;
        }
    }
}
