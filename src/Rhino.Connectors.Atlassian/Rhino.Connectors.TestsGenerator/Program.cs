using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.Xray;
using Rhino.Connectors.Xray.Cloud;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rhino.Connectors.TestsGenerator
{
    internal static class Program
    {
        // TODO: move to configuration
        private const int NumberOfTests = 1100;
        //private const string TestSetKey = "RA-62";
        //private const string Collection = "https://rhinoapi.atlassian.net";
        //private const string Project = "RA";
        //private const string User = "rhino.api@gmail.com";
        //private const string Password = "0hshf1gBkfZqsoABp9oO173D";

        private const string TestSetKey = "RA-1";
        private const string Collection = "http://localhost:8080";
        private const string Project = "RA";
        private const string User = "admin";
        private const string Password = "admin";

        private static IDictionary<string, object> capabilities = new Dictionary<string, object>
        {
            ["dryRun"] = false,
            ["bucketSize"] = 8
        };

        private static void Main(string[] args)
        {
            // setup
            var connecotrConfiguration = new RhinoConnectorConfiguration
            {
                Collection = Collection,
                Connector = Connector.JiraXryCloud,
                Password = Password,
                User = User,
                Project = Project
            };
            var configuration = new RhinoConfiguration
            {
                TestsRepository = new[] { "" },
                ConnectorConfiguration = connecotrConfiguration
            };
            var testCaseTemplate = new RhinoTestCase
            {
                TestSuites = new[] { TestSetKey },
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
                        Expected = "verify that {text} of {student_last_name_1} using {id} match {Alexander}"
                    },
                    new RhinoTestStep
                    {
                        Action = "close browser"
                    }
                }
            };

            // TODO: create factory for different connectors
            //var connector = new XrayCloudConnector(configuration);
            var connector = new XrayConnector(configuration);

            // send
            var options = new ParallelOptions { MaxDegreeOfParallelism = 8 };
            Parallel.For(0, NumberOfTests, options, (_) =>
            {
                testCaseTemplate.Scenario = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Demo Rhino Test Case";
                var issue = JToken.Parse(connector.ProviderManager.CreateTestCase(testCaseTemplate));
                Console.WriteLine($"Test {issue["id"]} created");
            });
        }
    }
}
