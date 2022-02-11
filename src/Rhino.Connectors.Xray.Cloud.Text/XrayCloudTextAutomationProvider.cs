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
using Rhino.Connectors.Text;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.Cloud.Text
{
    /// <summary>
    /// XRay connector for using XRay tests as Rhino Specs.
    /// </summary>
    public class XrayCloudTextAutomationProvider : ProviderManager
    {
        // state: global parameters
        private readonly ILogger logger;
        private readonly XrayCloudAutomationProvider xrayProvider;
        private readonly TextAutomationProvider textProvider;

        #region *** Constructors      ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        public XrayCloudTextAutomationProvider(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayCloudTextAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this provider.</param>
        public XrayCloudTextAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
            : base(configuration, types, logger)
        {
            this.logger = logger?.Setup(loggerName: nameof(XrayCloudTextAutomationProvider));
            xrayProvider = new XrayCloudAutomationProvider(configuration, types, this.logger);
            textProvider = new TextAutomationProvider(configuration, types, this.logger);
        }
        #endregion        

        #region *** Get: Test Cases   ***
        /// <summary>
        /// Returns a list of test cases for a project.
        /// </summary>
        /// <param name="ids">A list of issue id or key to get test cases by.</param>
        /// <returns>A collection of Rhino.Api.Contracts.AutomationProvider.RhinoTestCase</returns>
        public override IEnumerable<RhinoTestCase> OnGetTestCases(params string[] ids)
        {
            // setup
            var testCases = textProvider.OnGetTestCases(ids);

            // sync
            foreach (var testCase in testCases)
            {
                xrayProvider.UpdateTestCase(testCase);
            }

            // get
            return testCases.Any()
                ? xrayProvider.OnGetTestCases(testCases.Select(i => i.Key).ToArray())
                : xrayProvider.OnGetTestCases(ids);
        }
        #endregion

        #region *** Create: Test Case ***
        /// <summary>
        /// Creates a new test case under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider test case.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string OnCreateTestCase(RhinoTestCase testCase)
        {
            return xrayProvider.OnCreateTestCase(testCase);
        }
        #endregion

        #region *** Create: Test Run  ***
        /// <summary>
        /// Creates an automation provider test run entity. Use this method to implement the automation
        /// provider test run creation and to modify the loaded Rhino.Api.Contracts.AutomationProvider.RhinoTestRun.
        /// </summary>
        /// <param name="testRun">Rhino.Api.Contracts.AutomationProvider.RhinoTestRun object to modify before creating.</param>
        /// <returns>Rhino.Api.Contracts.AutomationProvider.RhinoTestRun based on provided test cases.</returns>
        public override RhinoTestRun OnCreateTestRun(RhinoTestRun testRun)
        {
            return xrayProvider.OnCreateTestRun(testRun);
        }

        /// <summary>
        /// Completes automation provider test run results, if any were missed or bypassed.
        /// </summary>
        /// <param name="testRun">Rhino.Api.Contracts.AutomationProvider.RhinoTestRun results object to complete by.</param>
        public override void OnRunTeardown(RhinoTestRun testRun) => xrayProvider.OnRunTeardown(testRun);
        #endregion

        #region *** Put: Test Results ***
        /// <summary>
        /// Updates a single test results iteration under automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update results.</param>
        public override void OnUpdateTestResult(RhinoTestCase testCase)
        {
            xrayProvider.OnUpdateTestResult(testCase);
        }
        #endregion

        #region *** Bugs & Defects    ***
        /// <summary>
        /// Gets a list of open bugs.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to find bugs.</param>
        /// <returns>A list of bugs (can be JSON or ID for instance).</returns>
        public override IEnumerable<string> OnGetBugs(RhinoTestCase testCase)
        {
            return xrayProvider.OnGetBugs(testCase);
        }

        /// <summary>
        /// Asserts if the RhinoTestCase has already an open bug.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to assert against match bugs.</param>
        /// <returns>An open bug.</returns>
        public override string OnGetOpenBug(RhinoTestCase testCase)
        {
            return xrayProvider.OnGetOpenBug(testCase);
        }

        /// <summary>
        /// Creates a new bug under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider bug.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string OnCreateBug(RhinoTestCase testCase)
        {
            return xrayProvider.OnCreateBug(testCase);
        }

        /// <summary>
        /// Executes a routine of post bug creation.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to execute routine on.</param>
        public override void OnCreateBugTeardown(RhinoTestCase testCase)
        {
            xrayProvider.OnCreateBugTeardown(testCase);
        }

        /// <summary>
        /// Updates an existing bug (partial updates are supported, i.e. you can submit and update specific fields only).
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update automation provider bug.</param>
        public override string OnUpdateBug(RhinoTestCase testCase)
        {
            return xrayProvider.OnUpdateBug(testCase);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override IEnumerable<string> OnCloseBugs(RhinoTestCase testCase)
        {
            return xrayProvider.OnCloseBugs(testCase);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override string OnCloseBug(RhinoTestCase testCase)
        {
            return xrayProvider.OnCloseBug(testCase);
        }
        #endregion
    }
}
