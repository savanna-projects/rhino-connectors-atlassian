/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
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
                var action = Regex.Replace(input: steps[i].Action, pattern: @"^\d+\.\s+", replacement: string.Empty);
                var requset = XpandCommandsRepository.CreateTestStep((id, key), action, steps[i].Expected, i);
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
        public static RhinoTestCase SetEvidences(this RhinoTestCase testCase)
        {
            try
            {
                // setup
                var forUploadOutcomes = new[] { "PASSED", "FAILED" };

                // exit conditions
                if (!forUploadOutcomes.Contains($"{testCase.Context["outcome"]}".ToUpper()))
                {
                    return testCase;
                }

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
            catch (Exception e) when (e != null)
            {
                // ignore exceptions
            }

            // get
            return testCase;
        }

        private static IEnumerable<IDictionary<string, object>> GetEvidence(RhinoTestCase testCase)
        {
            // get screenshots
            var screenshots = testCase.GetScreenshots();
            var automation = testCase.GetWebAutomation();
            var execution = $"{DoGetExecutionDetails(testCase).AsJObject().SelectToken("_id")}";

            // exit conditions
            if (!screenshots.Any() || automation == default)
            {
                return Array.Empty<IDictionary<string, object>>();
            }

            // get
            var evidences = GetEvidenceData(testCase);
            foreach (var evidence in evidences)
            {
                evidence["testRun"] = execution;
            }
            return evidences;
        }

        private static IEnumerable<IDictionary<string, object>> GetEvidenceData(RhinoTestCase testCase)
        {
            // setup
            var evidences = new List<IDictionary<string, object>>();
            var steps = testCase.Steps.ToList();

            // iterate
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i].Context.ContainsKey("runtimeid")
                    ? $"{steps[i].Context["runtimeid"]}"
                    : "-1";

                // get
                evidences.Add(new Dictionary<string, object>
                {
                    ["reference"] = i,
                    ["evidences"] = steps[i].GetScreenshots(),
                    ["step"] = step
                });
            }

            // get
            return evidences;
        }
        #endregion

        #region *** Actual Result    ***
        /// <summary>
        /// Updates test step actual result.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to update for.</param>
        public static RhinoTestCase SetActual(this RhinoTestCase testCase)
        {
            try
            {
                // setup
                var testRun = (testCase.Context["testRun"] as JToken).AsJObject();
                var id = $"{testRun.SelectToken("id")}";
                var key = $"{testRun.SelectToken("key")}";
                var run = $"{DoGetExecutionDetails(testCase).SelectToken("_id")}";
                var client = new XpandClient(testCase.GetAuthentication());
                var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };

                // update
                Parallel.ForEach(testCase.Steps, options, step => SetActual(step, client, (id, key), run));
            }
            catch (Exception e) when (e != null)
            {
                // ignore exceptions
            }

            // get
            return testCase;
        }

        private static void SetActual(RhinoTestStep step, XpandClient client, (string id, string key) idAndKey, string run)
        {
            try
            {
                if (step.Actual)
                {
                    return;
                }
                var actual = step.Exception == default
                    ? "{noformat}" + step.ReasonPhrase + "{noformat}"
                    : "{noformat}" + $"{step.Exception}" + "{noformat}";
                client.UpdateStepActual(idAndKey, run, ($"{step.Context["runtimeid"]}", actual));
            }
            catch (Exception e) when (e != null)
            {
                // ignore exceptions
            }
        }
        #endregion

        /// <summary>
        /// Gets execution details token from RhinoTestCase.Context.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to extract from.</param>
        /// <returns>Execution details token.</returns>
        public static JToken GetExecutionDetails(this RhinoTestCase testCase)
        {
            return DoGetExecutionDetails(testCase);
        }

        /// <summary>
        /// Sets test execution failure comment.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to update for.</param>
        public static RhinoTestCase SetFailedComment(this RhinoTestCase testCase)
        {
            try
            {
                // constants
                const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

                // setup
                var outcome = $"{testCase.Context["outcome"]}";

                // exit conditions
                var isFail = outcome.Equals("FAILED", Compare);
                if ((isFail && testCase.Actual) || !isFail)
                {
                    return testCase;
                }

                // setup
                var testRun = (testCase.Context["testRun"] as JToken).AsJObject();
                var id = $"{(testCase.Context["executionDetails"] as JToken)?.AsJObject().SelectToken("_id")}";
                var key = $"{testRun.SelectToken("key")}";
                var client = new XpandClient(testCase.GetAuthentication());

                // send
                var comment = testCase.GetFailComment();
                client.SetCommentOnExecution((id, key), comment);
            }
            catch (Exception e) when (e != null)
            {
                // ignore exceptions
            }

            // get
            return testCase;
        }

        /// <summary>
        /// Sets test execution inconclusive comment.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to update for.</param>
        public static RhinoTestCase SetInconclusiveComment(this RhinoTestCase testCase)
        {
            try
            {
                // exit conditions
                if (!testCase.Inconclusive)
                {
                    return testCase;
                }

                // setup
                var execution = $"{DoGetExecutionDetails(testCase).SelectToken("testExecIssueId")}";
                var jiraClient = new JiraClient(testCase.GetAuthentication());

                // set
                jiraClient.AddComment(
                    idOrKey: execution,
                    comment:
                        $"Test iteration {testCase.Iteration} on test run [{testCase.Key}] marked with default status by Rhino Engine." +
                        " Reason: test result is inconclusive.");
            }
            catch (Exception e) when (e != null)
            {
                // ignore exceptions
            }

            // get
            return testCase;
        }

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

        private static JToken DoGetExecutionDetails(RhinoTestCase testCase)
        {
            return testCase.Context.ContainsKey("executionDetails")
                ? testCase.Context["executionDetails"] as JToken
                : JToken.Parse("{}");
        }
    }
}