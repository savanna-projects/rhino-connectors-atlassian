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

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestRunExtensions
    {
        // constants
        private const string RavenTestsFormat = "/rest/raven/2.0/api/testexec/{0}/test";

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
    }
}
