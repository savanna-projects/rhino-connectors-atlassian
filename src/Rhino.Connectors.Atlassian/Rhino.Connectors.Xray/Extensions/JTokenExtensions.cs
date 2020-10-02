/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;

using System;
using System.Collections.Generic;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class JTokenExtensions
    {
        /// <summary>
        /// Converts test management test case interface into a RhinoTestCase.
        /// </summary>
        /// <param name="testCase">Test case token (from Jira response) to convert.</param>
        /// <returns>RhinoTestCase object.</returns>
        public static RhinoTestCase ToRhinoTestCase(this JToken testCase)
        {
            // initialize test case instance & fetch issues            
            var onTestCase = new RhinoTestCase();

            // apply context
            onTestCase.Context ??= new Dictionary<string, object>();
            onTestCase.Context[nameof(testCase)] = testCase;

            // fields: setup
            var priority = GetPriority(testCase);

            // fields: values
            onTestCase.Priority = string.IsNullOrEmpty(priority) ? onTestCase.Priority : priority;
            onTestCase.Key = $"{testCase["key"]}";
            onTestCase.Scenario = $"{testCase.SelectToken("fields.summary")}";
            onTestCase.Link = $"{testCase["self"]}";

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

                // normalize line breaks from XRay
                var onExpected = step.Expected.SplitByLines();
                step.Expected = string.Join(Environment.NewLine, onExpected);

                // apply
                step.Context[nameof(testStep)] = testStep;
                parsedSteps.Add(step);
            }

            // apply to connector test steps
            onTestCase.Steps = parsedSteps;
            return onTestCase;
        }

        private static string GetPriority(JToken testCase)
        {
            // setup conditions
            var priorityField = testCase.SelectToken("fields.priority");

            // exit conditions
            if(priorityField == default)
            {
                return string.Empty;
            }

            // setup priority
            return $"{priorityField["id"]} - {priorityField["name"]}";
        }
    }
}