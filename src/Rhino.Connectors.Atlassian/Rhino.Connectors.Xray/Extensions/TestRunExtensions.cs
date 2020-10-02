/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Framework;

using System;
using System.Linq;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestRunExtensions
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Gets all tests under this RhinoTestRun execution.
        /// </summary>
        /// <param name="testRun">RhinoTestRun by which to get tests.</param>
        public static JToken GetTests(this RhinoTestRun testRun)
        {
            // setup
            var executor = new JiraCommandsExecutor(testRun.GetAuthentication());

            // get results
            var response = RavenCommandsRepository.GetTestsByExecution(testRun.Key).Send(executor);

            // return all tests
            return response is JArray ? response : JToken.Parse("[]");
        }

        /// <summary>
        /// Close a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testRun">RhinoTestCase by which to close a bug.</param>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static void Close(this RhinoTestRun testRun, JiraClient jiraClient, string resolution)
        {
            // setup
            var transitions = jiraClient.GetTransitions(testRun.Key);
            var comment = $"{Api.Extensions.Utilities.GetActionSignature("closed")}";

            // exit conditions
            if (!transitions.Any())
            {
                return;
            }

            // get transition
            var transition = transitions.FirstOrDefault(i => i["to"].Equals("Closed", Compare));
            if (transition == default)
            {
                return;
            }

            //send transition
            jiraClient.CreateTransition(
                idOrKey: testRun.Key,
                transitionId: transition["id"],
                resolution: resolution,
                comment);
        }
    }
}