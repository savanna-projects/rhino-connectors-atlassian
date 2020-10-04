/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.Xray.Cloud.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

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
        public static string ToJiraCreateRequest(this RhinoTestCase testCase)
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
        /// <param name="testCase">Test case to create by.</param>
        /// <returns>A collection of create step request.</returns>
        public static IEnumerable<HttpCommand> ToXrayStepsCommands(this RhinoTestCase testCase)
        {
            // exit conditions
            Validate(testCase, "jira-issue-id");

            // setup
            var id = $"{testCase.Context["jira-issue-id"]}";
            var key = testCase.Key;
            var requests = new List<HttpCommand>();
            var steps = testCase.Steps.ToArray();

            // build
            for (int i = 0; i < steps.Length; i++)
            {
                var requset = XpandCommandsRepository.CreateTestStep((id, key), steps[i].Action, steps[i].Expected, i);
                requests.Add(requset);
            }

            // get
            return requests;
        }
        #endregion

        #region *** Set Outcome      ***
        /// <summary>
        /// Set XRay test execution results of test case by setting steps outcome.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update XRay results.</param>
        /// <returns>-1 if failed to update, 0 for success.</returns>
        /// <remarks>Must contain runtimeid field in the context.</remarks>
        public static int SetOutcome(this RhinoTestCase testCase)
        {
            //// get request content
            //var request = new
            //{
            //    steps = GetUpdateRequestObject(testCase)
            //};
            //var requestBody = JsonConvert.SerializeObject(request, JiraClient.JsonSettings);
            //var content = new StringContent(requestBody, Encoding.UTF8, JiraClient.MediaType);

            //// update fields
            //var route = string.Format(RavenRunFormat, $"{testCase.Context["runtimeid"]}");
            //var response = JiraClient.HttpClient.PutAsync(route, content).GetAwaiter().GetResult();

            //// results
            //if (!response.IsSuccessStatusCode)
            //{
            //    return -1;
            //}
            return 0;
        }

        private static List<object> GetUpdateRequestObject(RhinoTestCase testCase)
        {
            // add exceptions images - if exists or relevant
            //if (testCase.Context.ContainsKey(ContextEntry.OrbitResponse))
            //{
            //    testCase.AddExceptionsScreenshot();
            //}

            // collect steps
            var steps = new List<object>();
            //foreach (var testStep in testCase.Steps)
            //{
            //    steps.Add(testStep.GetUpdateRequest());
            //}
            return steps;
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
