/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESSOURCES
 */
using Newtonsoft.Json.Linq;
using Rhino.Api;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.Xray;
using Rhino.Connectors.Xray.Cloud;

using System;
using System.Threading.Tasks;

namespace Rhino.Connectors.TestsGenerator
{
    internal static class Program
    {
        // settings
        private const int numberOfTests = 1;
        private const string testSetKey = "App.Default.TestSetKey";
        private const string collection = "App.Default.Collection";
        private const string project = "App.Default.Project";
        private const string user = "App.Default.User";
        private const string password = "App.Default.Password";

        // state
        private static readonly RhinoConfiguration configuration = GetConfiguration();

        private static void Main()
        {
            // setup
            var testCaseTemplate = GetTestTemplate();
            var options = new ParallelOptions { MaxDegreeOfParallelism = 8/*App.Default.BucketSize*/ };

            // connector
            var connector = false//App.Default.ForCloud
                ? new XrayCloudConnector(configuration) as RhinoConnector
                : new XrayConnector(configuration);

            // send            
            Parallel.For(0, numberOfTests, options, (_) =>
            {
                testCaseTemplate.Scenario = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Demo Rhino Test Case";
                var issue = JToken.Parse(connector.ProviderManager.CreateTestCase(testCaseTemplate));
                Console.WriteLine($"Test {issue["id"]} created");
            });
        }

        private static RhinoConfiguration GetConfiguration()
        {
            // setup
            var connecotrConfiguration = new RhinoConnectorConfiguration
            {
                Collection = collection,
                Connector = false /*App.Default.ForCloud*/ ? RhinoConnectors.JiraXryCloud : RhinoConnectors.JiraXRay,
                Password = password,
                UserName = user,
                Project = project
            };

            // get
            return new RhinoConfiguration
            {
                TestsRepository = new[] { "" },
                ConnectorConfiguration = connecotrConfiguration
            };
        }

        private static RhinoTestCase GetTestTemplate() => new()
        {
            TestSuites = new[] { testSetKey },
            Steps = new[]
            {
                new RhinoTestStep
                {
                    Action = "go to url {https://gravitymvctestapplication.azurewebsites.net/student}",
                },
                new RhinoTestStep
                {
                    Action = "send keys {Carson} into {SearchString} using {id}"
                },
                new RhinoTestStep
                {
                    Action = "click on {SearchButton} using {id}",
                    ExpectedResults = new[]
                    {
                        new RhinoExpectedResult{ ExpectedResult = "verify that {text} of {student_last_name_1} using {id} match {Alexander}" },
                        new RhinoExpectedResult{ ExpectedResult = "verify that {text} of {student_first_name_1} using {id} match {Carson}" }
                    }
                },
                new RhinoTestStep
                {
                    Action = "close browser"
                }
            }
        };
    }
}
