/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.Xray.Cloud.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        #region *** Upload Evidences ***
        /// <summary>
        /// Upload evidences into an existing test execution.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by and into which to upload evidences.</param>
        public static void UploadEvidences(this RhinoTestCase testCase)
        {
            // setup
            var testRun = (testCase.Context["testRun"] as JToken).AsJObject();
            var id = $"{testRun.SelectToken("id")}";
            var key = $"{testRun.SelectToken("key")}";
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            // send
            var client = new XpandClient(testCase.GetAuthentication());
            Parallel.ForEach(GetEvidence(testCase), options, evidenceData =>
            {
                var evidences = evidenceData["evidences"] as List<string>;
                evidences ??= new List<string>();

                Parallel.ForEach(evidences, options, evidence
                    => client.CreateEvidence((id, key), $"{evidenceData["testRun"]}", $"{evidenceData["step"]}", evidence));
            });
        }

        private static IEnumerable<IDictionary<string, object>> GetEvidence(RhinoTestCase testCase)
        {
            // get screenshots
            var screenshots = testCase.GetScreenshots();
            var automation = testCase.GetWebAutomation();
            var execution = $"{GetExecutionDetails(testCase).AsJObject().SelectToken("_id")}";

            // exit conditions
            if (!screenshots.Any() || automation == default)
            {
                return Array.Empty<IDictionary<string, object>>();
            }

            // get
            var evidences = GetEvidenceData(testCase, screenshots);
            foreach (var evidence in evidences)
            {
                evidence["testRun"] = execution;
            }
            return evidences;
        }

        private static JToken GetExecutionDetails(RhinoTestCase testCase)
        {
            return testCase.Context.ContainsKey("executionDetails")
                ? testCase.Context["executionDetails"] as JToken
                : JToken.Parse("{}");
        }

        // TODO: clean
        private static IEnumerable<IDictionary<string, object>> GetEvidenceData(RhinoTestCase testCase, IEnumerable<string> screenshots)
        {
            // setup
            var evidences = new List<IDictionary<string, object>>();

            // iterate
            foreach (var screenshot in screenshots)
            {
                // setup
                var isReference = int.TryParse(Regex.Match(screenshot, @"(?<=-)\d+(?=-)").Value, out int referenceOut);
                if (!isReference)
                {
                    return Array.Empty<IDictionary<string, object>>();
                }

                // get attachment data
                var reference = testCase.GetActionReference(referenceOut).Reference;

                // check if exists
                var evidence = evidences.Find(i => (int)i["reference"] == reference);
                if (evidence != default)
                {
                    var images = evidence["evidences"] as List<string>;
                    images.Add(screenshot);
                    evidence["evidences"] = images;
                    continue;
                }

                var step = testCase.Steps.ElementAt(reference).Context.ContainsKey("runtimeid")
                    ? $"{testCase.Steps.ElementAt(reference).Context["runtimeid"]}"
                    : "-1";

                // get
                evidences.Add(new Dictionary<string, object>
                {
                    ["reference"] = reference,
                    ["evidences"] = new List<string>() { screenshot },
                    ["step"] = step
                });
            }

            // get
            return evidences;
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