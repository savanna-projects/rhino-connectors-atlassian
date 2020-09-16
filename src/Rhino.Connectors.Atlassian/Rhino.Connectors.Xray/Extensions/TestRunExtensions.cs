/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Newtonsoft.Json;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestRunExtensions
    {
        // constants
        private const string RavenTestsFormat = "/rest/raven/2.0/api/testexec/{0}/test";
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Gets all tests under this RhinoTestRun execution.
        /// </summary>
        /// <param name="testRun">RhinoTestRun by which to get tests.</param>
        public static IEnumerable<IDictionary<string, object>> GetTests(this RhinoTestRun testRun)
        {
            // setup
            var route = string.Format(RavenTestsFormat, testRun.Key);

            // get results
            var response = JiraClient.HttpClient.GetAsync(route).GetAwaiter().GetResult();

            // return all tests
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!responseBody.IsJson())
            {
                return Array.Empty<IDictionary<string, object>>();
            }
            return JsonConvert.DeserializeObject<IEnumerable<IDictionary<string, object>>>(responseBody);
        }

        /// <summary>
        /// Close a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <param name="testRun">RhinoTestCase by which to close a bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static bool Close(this RhinoTestRun testRun, JiraClient jiraClient, string resolution)
        {
            // setup
            var transitions = jiraClient.GetTransitions(testRun.Key);
            var comment = $"{Api.Extensions.Utilities.GetActionSignature("closed")}";

            // exit conditions
            if (!transitions.Any())
            {
                return false;
            }

            // get transition
            var transition = transitions.FirstOrDefault(i => i["to"].Equals("Closed", Compare));
            if (transition == default)
            {
                return false;
            }

            //send transition
            return jiraClient.Transition(
                issueKey: testRun.Key,
                transitionId: transition["id"],
                resolution: resolution,
                comment);
        }
    }
}
