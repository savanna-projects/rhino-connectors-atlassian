/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;

using System.Collections.Generic;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class JObjectExtensions
    {
        /// <summary>
        /// Converts test management test case interface into a RhinoTestCase.
        /// </summary>
        /// <param name="testCase">Test case token (from Jira response) to convert.</param>
        /// <returns>RhinoTestCase object.</returns>
        public static RhinoTestCase ToRhinoTestCase(this JObject testCase)
        {
            // initialize test case instance & fetch issues            
            var tesetCase = new RhinoTestCase();

            // apply context
            tesetCase.Context ??= new Dictionary<string, object>();
            tesetCase.Context[nameof(testCase)] = testCase;

            // fields
            tesetCase.Key = $"{testCase["key"]}";
            tesetCase.Scenario = $"{testCase.SelectToken("fields.summary")}";
            tesetCase.Link = $"{testCase["self"]}";

            // initialize test steps collection
            var testSteps = testCase.SelectToken("..steps");
            var parsedSteps = new List<RhinoTestStep>();

            // iterate test steps & normalize action/expected
            foreach (var testStep in testSteps.Children())
            {
                var step = new RhinoTestStep
                {
                    Action = $"{testStep["fields"]["Action"]}".Replace("{{", "{").Replace("}}", "}"),
                    Expected = $"{testStep["fields"]["Expected Result"]}".Replace("{{", "{").Replace("}}", "}")
                };
                step.Context[nameof(testStep)] = testStep;
                parsedSteps.Add(step);
            }

            // apply to connector test steps
            tesetCase.Steps = parsedSteps;
            return tesetCase;
        }
    }
}