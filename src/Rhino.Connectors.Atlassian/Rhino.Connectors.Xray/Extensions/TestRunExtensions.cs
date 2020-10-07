/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Framework;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestRunExtensions
    {
        /// <summary>
        /// Gets all tests under this RhinoTestRun execution.
        /// </summary>
        /// <param name="testRun">RhinoTestRun by which to get tests.</param>
        public static JToken GetTests(this RhinoTestRun testRun)
        {
            // setup
            var executor = new JiraCommandsExecutor(testRun.GetAuthentication());

            // get results
            var response = RavenCommandsRepository.GetTestsByExecution(testRun.Key).Send(executor).AsJToken();

            // return all tests
            return response is JArray ? response : JToken.Parse("[]");
        }
    }
}