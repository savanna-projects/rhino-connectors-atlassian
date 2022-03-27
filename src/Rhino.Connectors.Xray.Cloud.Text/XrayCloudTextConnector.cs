/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
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

namespace Rhino.Connectors.Xray.Cloud.Text
{
    /// <summary>
    /// XRay connector for running XRay tests as Rhino Automation Specs.
    /// </summary>
    [Connector(
        value: "ConnectorXrayCloudText",
        Name = "Connector - Atlassian XRay-Cloud and Text Connector",
        Description = "Allows to execute Rhino Specs from XRay-Cloud Test issues using Postman or Text Connector and report back as Test Execution issue.")]
    public class XrayCloudTextConnector : RhinoConnector
    {
        #region *** Constructors   ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        public XrayCloudTextConnector(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayCloudTextConnector(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Components.RhinoConnector.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this connector.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this connector.</param>
        public XrayCloudTextConnector(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
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
        public XrayCloudTextConnector(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger, bool connect)
            : base(configuration, types, logger)
        {
            // setup connector type (double check)
            configuration.ConnectorConfiguration ??= new RhinoConnectorConfiguration();
            configuration.ConnectorConfiguration.Connector = "ConnectorXrayCloud";

            // setup provider manager
            ProviderManager = new XrayCloudTextAutomationProvider(configuration, types, logger);

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
        protected override RhinoTestCase OnTestSetup(RhinoTestCase testCase)
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
        protected override RhinoTestCase OnTestTeardown(RhinoTestCase testCase)
        {
            // setup
            var outcome = testCase.Actual ? "PASS" : "FAIL";
            if (testCase.Inconclusive)
            {
                outcome = testCase.GetCapability(AtlassianCapabilities.InconclusiveStatus, "TODO");
            }

            // put
            testCase.Context["outcome"] = outcome;

            // update
            ProviderManager.UpdateTestResult(testCase);

            // return with results
            return testCase;
        }
        #endregion
    }
}
