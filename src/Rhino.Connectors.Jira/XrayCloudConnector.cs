/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESSOURCES
 * https://docs.getxray.app/display/XRAYCLOUD/REST+API
 * https://docs.getxray.app/display/XRAYCLOUD/Global+Settings%3A+API+Keys
 */
using Gravity.Abstraction.Logging;

using Rhino.Api;
using Rhino.Api.Contracts.Attributes;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;
using Utilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray.Cloud
{
    /// <summary>
    /// XRay connector for running XRay tests as Rhino Automation Specs.
    /// </summary>
    [Connector(
        value: RhinoConnectors.JiraXryCloud,
        Name = "Connector - Atlassian XRay, On-Cloud",
        Description = "Allows to execute Rhino Specs from XRay Test issues and report back as Test Execution issue.")]
    public class XrayCloudConnector : RhinoConnector
    {
        #region *** Constructors   ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        public XrayCloudConnector(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayCloudConnector(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this connector.</param>
        public XrayCloudConnector(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
            : this(configuration, types, logger, connect: true)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this connector.</param>
        /// <param name="connect"><see cref="true"/> for immediately connect after construct <see cref="false"/> skip connection.</param>
        /// <remarks>If you skip connection you must explicitly call Connect method.</remarks>
        public XrayCloudConnector(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger, bool connect)
            : base(configuration, types, logger)
        {
            // setup connector type (double check)
            configuration.ConnectorConfiguration ??= new RhinoConnectorConfiguration();
            configuration.ConnectorConfiguration.Connector = RhinoConnectors.JiraXryCloud;

            // setup provider manager
            ProviderManager = new XrayCloudAutomationProvider(configuration, types, logger);

            // connect on constructing
            if (connect)
            {
                Connect();
            }
        }
        #endregion

        #region *** Per Test Setup ***
        /// <summary>
        /// Performed just before each test is called.
        /// </summary>
        /// <param name="testCase">The Rhino.Api.Contracts.AutomationProvider.RhinoTestCase which is being executed.</param>
        public override RhinoTestCase OnTestSetup(RhinoTestCase testCase)
        {
            // setup
            testCase.Context["outcome"] = "EXECUTING";

            // update
            ProviderManager.UpdateTestResult(testCase);

            // return with results
            return testCase;
        }
        #endregion

        #region *** Per Test Clean ***
        /// <summary>
        /// Performed just after each test is called.
        /// </summary>
        /// <param name="testCase">The Rhino.Api.Contracts.AutomationProvider.RhinoTestCase which was executed.</param>
        public override RhinoTestCase OnTestTeardown(RhinoTestCase testCase)
        {
            // constants
            const string Updated = "runUpdated";

            // setup
            var outcome = testCase.Actual ? "PASSED" : "FAILED";
            var alreadyFail = ProviderManager
                .TestRun
                .TestCases
                .Any(i => i.Key == testCase.Key && !i.Actual && i.Context.ContainsKey(Updated) && (bool)i.Context[Updated]);

            // failed on this run (mark as fail)
            if (alreadyFail)
            {
                outcome = "FAILED";
            }

            // inconclusive on this run (mark as default)
            if (ProviderManager.TestRun.TestCases.Any(i => i.Key.Equals(testCase.Key) && i.Inconclusive))
            {
                outcome = testCase.GetCapability(AtlassianCapabilities.InconclusiveStatus, "TODO");
            }

            // put
            testCase.Context["outcome"] = outcome;

            // update results
            ProviderManager.UpdateTestResult(testCase);


            // return with results
            return testCase;
        }
        #endregion
    }
}
