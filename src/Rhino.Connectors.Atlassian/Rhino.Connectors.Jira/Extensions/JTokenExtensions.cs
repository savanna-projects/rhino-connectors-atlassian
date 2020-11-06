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
using System.Text.RegularExpressions;

namespace Rhino.Connectors.Xray.Cloud.Extensions
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
                var parsedStep = ParseStep(testStep);
                parsedSteps.Add(parsedStep);
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
            if (priorityField == default)
            {
                return string.Empty;
            }

            // setup priority
            return $"{priorityField["id"]} - {priorityField["name"]}";
        }

        private static RhinoTestStep ParseStep(JToken testStep)
        {
            // setup
            var step = GetTestStep(testStep);

            // normalize auto links (if any)
            step.Action = NormalizeAutoLink(step.Action);
            step.Expected = NormalizeAutoLink(step.Expected);

            // normalize line breaks from XRay
            var onExpected = step.Expected.SplitByLines();
            step.Expected = string.Join(Environment.NewLine, onExpected);

            // apply
            step.Context[nameof(testStep)] = testStep;
            return step;
        }

        private static RhinoTestStep GetTestStep(JToken testStep)
        {
            // constants
            const string Pattern = @"{{(?!\$).*?}}";

            // 1st cycle
            var rhinoStep = new RhinoTestStep
            {
                Action = $"{testStep["action"]}".Replace(@"\{", "{").Replace(@"\[", "[").Replace("{{{{", "{{").Replace("}}}}", "}}"),
                Expected = $"{testStep["result"]}".Replace(@"\{", "{").Replace(@"\[", "[").Replace("{{{{", "{{").Replace("}}}}", "}}")
            };

            // 2nd cycle: action
            var matches = Regex.Matches(rhinoStep.Action, Pattern);
            foreach (Match match in matches)
            {
                rhinoStep.Action = ReplaceByMatch(rhinoStep.Action, match);
            }

            // 3rd cycle: action
            matches = Regex.Matches(rhinoStep.Expected, Pattern);
            foreach (Match match in matches)
            {
                rhinoStep.Expected = ReplaceByMatch(rhinoStep.Expected, match);
            }

            // get
            return rhinoStep;
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

        private static string ReplaceByMatch(string input, Match match)
        {
            // setup
            var nValue = match.Value.Replace("{{", "{").Replace("}}", "}");

            // replace
            return input.Replace(match.Value, nValue);
        }
    }
}