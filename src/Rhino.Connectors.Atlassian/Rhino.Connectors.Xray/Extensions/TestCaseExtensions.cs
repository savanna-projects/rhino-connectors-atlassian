/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;
using Gravity.Services.DataContracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Connectors.AtlassianClients;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestCaseExtensions
    {
        // members: constants
        private const string RavenExecutionFormat = "/rest/raven/2.0/api/testrun/?testExecIssueKey={0}&testIssueKey={1}";
        private const string RavenRunFormat = "/rest/raven/2.0/api/testrun/{0}";
        private const string RavenAttachmentFormat = "/rest/raven/2.0/api/testrun/{0}/step/{1}/attachment";

        // JSON Settings
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        /// <summary>
        /// Set XRay runtime ids on all steps under this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase on which to update runtime ids.</param>
        /// <param name="testExecutionKey">Jira test execution key by which to find runtime ids.</param>
        public static void SetRuntimeKeys(this RhinoTestCase testCase, string testExecutionKey)
        {
            // add test step id into test-case context
            var route = string.Format(RavenExecutionFormat, testExecutionKey, testCase.Key);
            var response = JiraClient.HttpClient.GetAsync(route).GetAwaiter().GetResult();

            // exit conditions
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            // setup
            var jsonToken = response.ToObject()["steps"];
            var stepsToken = JArray.Parse($"{jsonToken}");
            var stepsCount = testCase.Steps.Count();

            // apply runtime id to test-step context
            for (int i = 0; i < stepsCount; i++)
            {
                testCase.Steps.ElementAt(i).Context["runtimeid"] = stepsToken[i]["id"].ToObject<long>();
            }

            // apply test run key
            testCase.Context["testRunKey"] = testExecutionKey;
            testCase.Context["runtimeid"] = DoGetExecutionRuntime(testCase);
        }

        /// <summary>
        /// Gets the runtime id of the test execution this test belongs to.
        /// </summary>
        /// <param name="testCase">RhinoTestCase for which to get runtime id.</param>
        /// <returns>XRay runtime id of the test execution this RhinoTestCase belongs to.</returns>
        public static int GetExecutionRuntime(this RhinoTestCase testCase)
        {
            return DoGetExecutionRuntime(testCase);
        }

        /// <summary>
        /// Updates test results comment.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update test results.</param>
        public static void PutResultComment(this RhinoTestCase testCase, string comment)
        {
            // setup: routing
            var routing = string.Format(RavenRunFormat, testCase.Context["runtimeid"]);

            // setup: content
            var requestBody = JsonConvert.SerializeObject(new { Comment = comment }, JsonSettings);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // update
            JiraClient.HttpClient.PutAsync(routing, content).GetAwaiter().GetResult();
            Thread.Sleep(1000);
        }

        /// <summary>
        /// Gets a text structure explaining why this test failed. Can be used for comments.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by from which to build text structure.</param>
        /// <returns>text structure explaining why this test failed.</returns>
        public static string GetFailComment(this RhinoTestCase testCase)
        {
            return DoGetFailComment(testCase);
        }

        #region *** Set Outcome      ***
        /// <summary>
        /// Set XRay test execution results of test case by setting steps outcome.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update XRay results.</param>
        /// <param name="outcome"></param>
        /// <returns>-1 if failed to update, 0 for success.</returns>
        /// <remarks>Must contain runtimeid field in the context.</remarks>
        public static int SetOutcome(this RhinoTestCase testCase, string outcome)
        {
            // get request content
            var request = new
            {
                steps = GetUpdateRequestObject(testCase, outcome)
            };
            var requestBody = JsonConvert.SerializeObject(request, JsonSettings);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // update fields
            var route = string.Format(RavenRunFormat, $"{testCase.Context["runtimeid"]}");
            var response = JiraClient.HttpClient.PutAsync(route, content).GetAwaiter().GetResult();

            // results
            if (!response.IsSuccessStatusCode)
            {
                return -1;
            }
            return 0;
        }

        private static List<object> GetUpdateRequestObject(RhinoTestCase testCase, string outcome)
        {
            // add exceptions images - if exists or relevant
            if (testCase.Context.ContainsKey(ContextEntry.OrbitResponse))
            {
                testCase.AddExceptionsScreenshot();
            }

            // collect steps
            var steps = new List<object>();
            foreach (var testStep in testCase.Steps)
            {
                steps.Add(testStep.GetUpdateRequest(outcome));
            }
            return steps;
        }
        #endregion

        #region *** Upload Evidences ***
        /// <summary>
        /// Upload evidences into an existing test execution.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by and into which to upload evidences.</param>
        public static void UploadEvidences(this RhinoTestCase testCase)
        {
            DoUploadtEvidences(testCase);
        }
        #endregion

        // UTILITIES
        // execution runtime id
        private static int DoGetExecutionRuntime(RhinoTestCase testCase)
        {
            // exit conditions
            if (testCase == default)
            {
                return default;
            }

            // get test run from JIRA
            var routing = string.Format(RavenExecutionFormat, testCase.Context["testRunKey"], testCase.Key);
            var httpResponseMessage = JiraClient.HttpClient.GetAsync(routing).GetAwaiter().GetResult();

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return 0;
            }
            int.TryParse($"{httpResponseMessage.ToObject()["id"]}", out int idOut);
            return idOut;
        }

        // upload evidences
        private static void DoUploadtEvidences(this RhinoTestCase testCase)
        {
            // setup
            var run = 0;
            if (testCase.Context.ContainsKey("runtimeid"))
            {
                int.TryParse($"{testCase.Context["runtimeid"]}", out run);
            }

            // upload
            foreach (var (Id, Data) in GetEvidence(testCase))
            {
                var route = string.Format(RavenAttachmentFormat, run, Id);
                var content = new StringContent(JsonConvert.SerializeObject(Data), Encoding.UTF8, "application/json");
                JiraClient.HttpClient.PostAsync(route, content).GetAwaiter().GetResult();
            }
        }

        private static IEnumerable<(long Id, IDictionary<string, object> Data)> GetEvidence(RhinoTestCase testCase)
        {
            // get screenshots
            var screenshots = ((OrbitResponse)testCase.Context[ContextEntry.OrbitResponse])
                .OrbitRequest
                .Screenshots
                .Select(i => i.Location);

            // get for step
            var evidences = new ConcurrentBag<(long, IDictionary<string, object>)>();
            foreach (var screenshot in screenshots)
            {
                // setup
                var isReference = int.TryParse(Regex.Match(screenshot, @"(?<=-)\d+(?=-)").Value, out int referenceOut);
                if (!isReference)
                {
                    continue;
                }

                // get attachment requests for test case
                var reference = GetStepReference(((WebAutomation)testCase.Context[ContextEntry.WebAutomation]).Actions, referenceOut);
                var evidence = GetEvidenceBody(screenshot);
                evidences.Add(((long)testCase.Steps.ElementAt(reference).Context["runtimeid"], evidence));
            }

            // results
            return evidences;
        }

        private static int GetStepReference(IEnumerable<ActionRule> actions, int reference)
        {
            if (actions.ElementAt(reference).ActionType == ActionType.CloseBrowser || reference < 0)
            {
                return -1;
            }
            if (actions.ElementAt(reference).ActionType != ActionType.Assert)
            {
                return reference;
            }
            return GetStepReference(actions, reference - 1);
        }

        private static IDictionary<string, object> GetEvidenceBody(string screenshot)
        {
            // standalone
            return new Dictionary<string, object>
            {
                ["filename"] = Path.GetFileName(screenshot),
                ["contentType"] = "image/png",
                ["data"] = Convert.ToBase64String(File.ReadAllBytes(screenshot))
            };
        }

        // get comments for failed tests
        private static string DoGetFailComment(RhinoTestCase testCase)
        {
            // setup
            var failedSteps = testCase.Steps.Where(i => !i.Actual).Select(i => ((JToken)i.Context["testStep"])["index"]);

            // exit conditions
            if (!failedSteps.Any())
            {
                return string.Empty;
            }

            // build
            var comment = new StringBuilder();
            comment
                .Append("{noformat}")
                .Append(DateTime.Now)
                .Append(": Test [")
                .Append(testCase.Key)
                .Append("] Failed on iteration [")
                .Append(testCase.Iteration)
                .Append("] ")
                .Append("Steps [")
                .Append(string.Join(",", failedSteps))
                .AppendLine("]")
                .AppendLine()
                .AppendLine("[Driver Parameters]")
                .AppendLine(JsonConvert.SerializeObject(testCase.Context[ContextEntry.DriverParams], JsonSettings))
                .AppendLine()
                .AppendLine("[Local Data Source]")
                .Append(JsonConvert.SerializeObject(testCase.DataSource, JsonSettings))
                .AppendLine("{noformat}");

            // return
            return comment.ToString();
        }
    }
}