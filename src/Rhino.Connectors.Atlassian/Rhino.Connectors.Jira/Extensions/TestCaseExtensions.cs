/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json;

using Rhino.Api.Contracts.AutomationProvider;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace Rhino.Connectors.Xray.Cloud.Extensions
{
    internal static class TestCaseExtensions
    {
        #region *** To Issue Request ***
        /// <summary>
        /// Converts RhinoTestCase to create test issue request.
        /// </summary>
        /// <param name="testCase">Test case to convert.</param>
        /// <returns>Create test issue request.</returns>
        public static string ToJiraCreateExecutionRequest(this RhinoTestCase testCase)
        {
            // exit conditions
            Validate(testCase, "issuetype-id", "project-key");

            // field: steps > description
            var description = testCase.Context.ContainsKey("description")
                ? testCase.Context["description"]
                : string.Empty;

            // compose json
            var requestBody = new Dictionary<string, object>
            {
                ["summary"] = testCase.Scenario,
                ["description"] = description,
                ["issuetype"] = new Dictionary<string, object>
                {
                    ["id"] = $"{testCase.Context["issuetype-id"]}"
                },
                ["project"] = new Dictionary<string, object>
                {
                    ["key"] = $"{testCase.Context["project-key"]}"
                }
            };
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["fields"] = requestBody
            });
        }

        /// <summary>
        /// Converts RhinoTestCase to create test steps requests collection.
        /// </summary>
        /// <param name="testCase">Test case to convert.</param>
        /// <returns>Create test issue request.</returns>
        public static IEnumerable<(string Endpoint, StringContent Content)> ToXrayStepsRequests(this RhinoTestCase testCase)
        {
            // exit conditions
            Validate(testCase, "jira-issue-id");

            // setup
            var endpoint = $"/api/internal/test/{testCase.Context["jira-issue-id"]}/step";
            var results = new List<(string Endpoint, StringContent Content)>();
            var steps = testCase.Steps.ToArray();

            for (int i = 0; i < steps.Length; i++)
            {
                // setup body
                var requestObjt = new Dictionary<string, object>
                {
                    ["action"] = steps[i].Action,
                    ["result"] = steps[i].Expected,
                    ["index"] = i
                };
                var requestBody = JsonConvert.SerializeObject(requestObjt);

                // setup content
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // apply
                results.Add((endpoint, content));
            }

            // results
            return results;
        }
        #endregion

        // VALIDATION UTILITY
        private static void Validate(RhinoTestCase testCase, params string[] keys)
        {
            foreach (var key in keys)
            {
                Validate(testCase.Context, key);
            }
        }

        private static void Validate(IDictionary<string, object> context, string key)
        {
            // constants
            const string L = "https://developer.atlassian.com/server/jira/platform/jira-rest-api-examples/";
            const string M =
                "TestCase.Context dictionary must have a key [{0}] with a valid value. Please check [{1}] for available values.";

            // exit conditions
            if (context.ContainsKey(key))
            {
                return;
            }

            // exception
            var message = string.Format(M, key, L);
            throw new InvalidOperationException(message) { HelpLink = L };
        }
    }
}
