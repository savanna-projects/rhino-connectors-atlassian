﻿/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients.Extensions;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.Xray.Extensions
{
    public static class JTokenExtensions
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
            var testCaseObject = testCase.ConvertToJObject();

            // apply context
            onTestCase.Context ??= new Dictionary<string, object>();
            onTestCase.Context[nameof(testCase)] = $"{testCaseObject}";

            // fields: setup
            var priority = GetPriority(testCaseObject);

            // fields: values
            onTestCase.Priority = string.IsNullOrEmpty(priority) ? onTestCase.Priority : priority;
            onTestCase.Key = $"{testCaseObject.SelectToken("key")}";
            onTestCase.Scenario = $"{testCaseObject.SelectToken("fields.summary")}";
            onTestCase.Link = $"{testCaseObject.SelectToken("self")}";

            // initialize test steps collection
            var testSteps = testCaseObject.SelectToken("..steps");
            var parsedSteps = new List<RhinoTestStep>();

            // iterate test steps & normalize action/expected
            foreach (var testStep in testSteps.Children())
            {
                var step = GetTestStep(testStep);

                // normalize auto links (if any)
                step.Action = NormalizeAutoLink(step.Action);
                foreach (var result in step.ExpectedResults)
                {
                    result.ExpectedResult = NormalizeAutoLink(result.ExpectedResult);
                }

                // apply
                step.Context[nameof(testStep)] = $"{testStep}";
                parsedSteps.Add(step);
            }

            // apply to connector test steps
            onTestCase.Steps = parsedSteps;
            return onTestCase;
        }

        private static string GetPriority(JObject testCase)
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

        private static string NormalizeAutoLink(string input)
        {
            // setup
            var match = Regex.Match(input, pattern: @"(?<=\{\[)[^\]]*(?=]\})");

            // exit conditions
            if (string.IsNullOrEmpty(match.Value))
            {
                return input;
            }

            // enqueue
            var queue = new Queue<Match>();
            queue.Enqueue(match);

            // iterate
            while (queue.Count > 0)
            {
                var onMatch = queue.Dequeue();
                var segmeants = onMatch.Value.Split('|');

                if (segmeants.Length <= 1)
                {
                    return onMatch.Value;
                }
                input = input.Replace($"[{onMatch.Value}]", segmeants[1]);

                match = Regex.Match(input, pattern: @"(?<=\{\[)[^\]]*(?=]\})");
                if (!string.IsNullOrEmpty(match.Value))
                {
                    queue.Enqueue(match);
                }
            }
            return input;
        }

        private static RhinoTestStep GetTestStep(JToken testStep)
        {
            // constants
            const string Pattern = @"{{(?!\$).*?}}";

            // 1st cycle
            var action = $"{testStep["fields"]["Action"]}".Replace(@"\{", "{").Replace(@"\[", "[").Replace("{{{{", "{{").Replace("}}}}", "}}");
            var expected = $"{testStep["fields"]["Expected Result"]}".Replace(@"\{", "{").Replace(@"\[", "[").Replace("{{{{", "{{").Replace("}}}}", "}}");
            var rhinoStep = new RhinoTestStep
            {
                Action = action,
                ExpectedResults = expected
                    .SplitByLines()
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Select(i => new RhinoExpectedResult { ExpectedResult = i.Trim() })
            };

            // 2nd cycle: action
            var matches = Regex.Matches(rhinoStep.Action, Pattern);
            foreach (Match match in matches)
            {
                rhinoStep.Action = ReplaceByMatch(rhinoStep.Action, match);
            }

            // 3rd cycle: action
            foreach (var result in rhinoStep.ExpectedResults)
            {
                matches = Regex.Matches(result.ExpectedResult, Pattern);
                foreach (Match match in matches)
                {
                    result.ExpectedResult = ReplaceByMatch(result.ExpectedResult, match);
                }
            }


            // get
            return rhinoStep;
        }

        private static string ReplaceByMatch(string input, Match match)
        {
            // setup
            var nValue = match.Value.Replace("{{", "{").Replace("}}", "}");

            // replace
            return input.Replace(match.Value, nValue);
        }
    }
}
