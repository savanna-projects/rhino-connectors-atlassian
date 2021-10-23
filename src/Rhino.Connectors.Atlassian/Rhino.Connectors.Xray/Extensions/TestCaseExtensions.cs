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
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Framework;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.Xray.Extensions
{
    public static class TestCaseExtensions
    {
        // members: constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Set XRay runtime ids on all steps under this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase on which to update runtime ids.</param>
        /// <param name="testExecutionKey">Jira test execution key by which to find runtime ids.</param>
        public static void SetRuntimeKeys(this RhinoTestCase testCase, string testExecutionKey)
        {
            // setup
            var ravenClient = new JiraCommandsExecutor(testCase.GetAuthentication());
            var command = RavenCommandsRepository.GetTestRunExecutionDetails(testExecutionKey, testCase.Key);

            // send
            var response = ravenClient.SendCommand(command).AsJToken();

            // exit conditions
            if ($"{response["id"]}".Equals("-1"))
            {
                return;
            }

            // setup
            var jsonToken = response["steps"];
            var stepsToken = JArray.Parse($"{jsonToken}");
            var aggregated = testCase.AggregateSteps();
            var steps = aggregated.Steps.ToList();

            // apply runtime id to test-step context
            for (int i = 0; i < steps.Count; i++)
            {
                var isKey = steps[i].Context.ContainsKey(ContextEntry.ChildSteps);
                var isType = isKey && steps[i].Context[ContextEntry.ChildSteps] is IEnumerable<RhinoTestStep>;
                if (isType)
                {
                    foreach (var _step in (IEnumerable<RhinoTestStep>)steps[i].Context[ContextEntry.ChildSteps])
                    {
                        _step.Context["testStep"] = $"{stepsToken[i]}";
                        _step.Context["runtimeid"] = stepsToken[i]["id"].ToObject<long>();
                    }
                }
            }

            // apply test run key
            _ = int.TryParse($"{response["id"]}", out int idOut);
            testCase.Context["runtimeid"] = idOut;
            testCase.Context["testRunKey"] = testExecutionKey;
            testCase.Context["aggregated"] = aggregated;
            aggregated.Steps = steps;
        }

        /// <summary>
        /// Updates test results comment.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update test results.</param>
        public static void UpdateResultComment(this RhinoTestCase testCase, string comment)
        {
            // setup
            var executor = new JiraCommandsExecutor(testCase.GetAuthentication());
            var command = RavenCommandsRepository.UpdateTestRun(
                testRun: $"{testCase.Context["runtimeid"]}",
                data: new { Comment = comment });

            // send
            executor.SendCommand(command);
        }

        public static IEnumerable<string> GetTestPlans(this RhinoTestCase testCase)
        {
            // setup
            const string key = "testPlans";

            // not found
            if (!testCase.Context.ContainsKey(key))
            {
                return Array.Empty<string>();
            }

            // get
            return System.Text.Json.JsonSerializer
                .Deserialize<IEnumerable<string>>($"{testCase.Context[key]}", jsonOptions);
        }

        #region *** To Issue Request ***
        /// <summary>
        /// Converts connector test step interface into a test management test step.
        /// </summary>
        /// <param name="testStep">Test step to convert.</param>
        /// <param name="projectRuntimeKey">The test runtime key to apply steps to.</param>
        /// <param name="rank">The rank of the test step (location order).</param>
        /// <returns>Test management test step.</returns>
        public static string ToJiraXrayIssue(this RhinoTestStep testStep, string projectRuntimeKey, int rank)
        {
            // setup
            var action = Regex
                .Replace(input: testStep.Action.Replace("{", "{{").Replace("}", "}}"), pattern: @"^(\s+)?(\d+)\.", replacement: string.Empty)
                .Trim();
            var expected = string.IsNullOrEmpty(testStep.Expected)
                ? string.Empty
                : testStep.Expected.Replace("{", "{{").Replace("}", "}}").Trim();
            
            // build
            var payload = new Dictionary<string, object>
            {
                ["rank"] = rank,
                ["fields"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["@type"] = "nativeValue",
                        ["customField"] = new Dictionary<string, object>
                        {
                            ["id"] = $"STEP_NATIVE_{projectRuntimeKey}",
                            ["rank"] = 1,
                            ["projectId"] = "",
                            ["type"] = "NATIVE",
                            ["entity"] = "TEST_STEP"
                        },
                        ["wikiField"] = new Dictionary<string, object>
                        {
                            ["raw"] = action
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["@type"] = "nativeValue",
                        ["customField"] = new Dictionary<string, object>
                        {
                            ["id"] = $"RESULT_NATIVE_{projectRuntimeKey}",
                            ["rank"] = 3,
                            ["projectId"] = "",
                            ["type"] = "NATIVE",
                            ["entity"] = "TEST_STEP"
                        },
                        ["wikiField"] = new Dictionary<string, object>
                        {
                            ["raw"] = expected
                        }
                    },
                }
            };

            // get
            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Converts connector test case interface into a test management test case.
        /// </summary>
        /// <param name="testCase">Test case to convert</param>
        /// <returns>Test management test case</returns>
        public static string ToJiraXrayIssue(this RhinoTestCase testCase)
        {
            return InvokeToJiraXrayIssue(testCase, includeSteps: true);
        }

        /// <summary>
        /// Converts connector test case interface into a test management test case.
        /// </summary>
        /// <param name="testCase">Test case to convert</param>
        /// <returns>Test management test case</returns>
        public static string ToJiraXrayIssue(this RhinoTestCase testCase, bool includeSteps)
        {
            return InvokeToJiraXrayIssue(testCase, includeSteps);
        }

        private static string InvokeToJiraXrayIssue(RhinoTestCase testCase, bool includeSteps)
        {
            // exit conditions
            ValidationXray(testCase);

            // field: steps > description
            var steps = GetSteps(testCase);
            var description = testCase.Context.ContainsKey("description")
                ? testCase.Context["description"]
                : string.Empty;

            // compose json
            var payload = new Dictionary<string, object>
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
                },
                [$"{testCase.Context["manual-test-steps-custom-field"]}"] = includeSteps
                    ? new Dictionary<string, object> { ["steps"] = steps }
                    : new Dictionary<string, object>()
            };

            // priority
            if (testCase.Context.ContainsKey("test-priority"))
            {
                payload["priority"] = new Dictionary<string, object>
                {
                    ["id"] = $"{testCase.Context["test-priority"]}"
                };
            }

            // test suite
            if (testCase.TestSuites.Any())
            {
                payload[$"{testCase.Context["test-sets-custom-field"]}"] = testCase.TestSuites;
            }

            // test plan
            var testPlans = testCase.GetCapability(capability: AtlassianCapabilities.TestPlans, defaultValue: Array.Empty<string>());
            var isTestPlan = testPlans.Length != 0 && testCase.Context.ContainsKey("test-plan-custom-field");
            if (isTestPlan)
            {
                payload[$"{testCase.Context["test-plan-custom-field"]}"] = testPlans;
            }

            // jira custom fields
            if (testCase.Context.ContainsKey("jira-custom-fields"))
            {
                var jiraCustomFields = testCase.Context["jira-custom-fields"] as IDictionary<string, object>;
                foreach (var jiraCustomField in jiraCustomFields)
                {
                    payload[jiraCustomField.Key] = jiraCustomField.Value;
                }
            }

            // get
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["fields"] = payload
            });
        }

        private static void ValidationXray(RhinoTestCase testCase)
        {
            Validate(testCase.Context, "issuetype-id");
            Validate(testCase.Context, "project-key");
            Validate(testCase.Context, "manual-test-steps-custom-field");
            Validate(testCase.Context, "test-sets-custom-field");
        }

        private static void Validate(IDictionary<string, object> context, string key)
        {
            // constants
            const string Link = "https://developer.atlassian.com/server/jira/platform/jira-rest-api-examples/";
            const string Format = "Find-Key -Key [{0}] -Context [{1}] -Method [{2}] = false";

            // exit conditions
            if (context.ContainsKey(key))
            {
                return;
            }

            // exception
            var message = string.Format(Format, key, nameof(context), nameof(Validate));
            throw new InvalidOperationException(message) { HelpLink = Link };
        }

        private static IEnumerable<IDictionary<string, object>> GetSteps(RhinoTestCase testCase)
        {
            var steps = new List<IDictionary<string, object>>();
            for (int i = 0; i < testCase.Steps.Count(); i++)
            {
                // setup conditions
                var isAction = !string.IsNullOrEmpty(testCase.Steps.ElementAt(i).Action);
                if (!isAction)
                {
                    continue;
                }

                var onStep = testCase.Steps.ElementAt(i);
                var step = new Dictionary<string, object>
                {
                    ["id"] = i + 1,
                    ["index"] = i + 1,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Action"] = onStep.Action.Replace("{", "{{").Replace("}", "}}"),
                        ["Expected Result"] = string.IsNullOrEmpty(onStep.Expected) ? string.Empty : onStep.Expected.Replace("{", "{{").Replace("}", "}}")
                    }
                };
                // add to steps list
                steps.Add(step);
            }
            return steps;
        }
        #endregion

        #region *** Set Outcome      ***
        /// <summary>
        /// Set XRay test execution results of test case by setting steps outcome.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update XRay results.</param>
        /// <returns>-1 if failed to update, 0 for success.</returns>
        /// <remarks>Must contain runtimeid field in the context.</remarks>
        public static void SetOutcomeBySteps(this RhinoTestCase testCase)
        {
            const string Aggregated = "aggregated";

            // get steps
            var onTestCase = testCase.AggregateSteps();
            onTestCase.Context.AddRange(testCase.Context, new[] { Aggregated });

            // collect steps
            var steps = new List<object>();
            foreach (var testStep in onTestCase.Steps)
            {
                steps.Add(testStep.GetUpdateRequest(outcome: $"{onTestCase.Context["outcome"]}"));
            }

            // setup
            var executor = new JiraCommandsExecutor(onTestCase.GetAuthentication());
            var command = RavenCommandsRepository.UpdateTestRun(
                testRun: $"{onTestCase.Context["runtimeid"]}",
                data: new { Steps = steps });

            // send
            executor.SendCommand(command);
        }

        /// <summary>
        /// Set XRay test execution results of test case by setting steps outcome.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update XRay results.</param>
        /// <remarks>Must contain runtimeid field in under RhinoTestCase.Context.</remarks>
        public static void SetOutcomeByRun(this RhinoTestCase testCase)
        {
            DoSetOutcomeByRun(testCase, $"{testCase.Context["outcome"]}");
        }

        /// <summary>
        /// Set XRay test execution results of test case by setting steps outcome.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update XRay results.</param>
        /// <param name="outcome">Test run outcome to set.</param>
        /// <remarks>Must contain runtimeid field in under RhinoTestCase.Context.</remarks>
        public static void SetOutcomeByRun(this RhinoTestCase testCase, string outcome)
        {
            DoSetOutcomeByRun(testCase, outcome);
        }

        private static void DoSetOutcomeByRun(RhinoTestCase testCase, string outcome)
        {
            // setup
            var executor = new JiraCommandsExecutor(testCase.GetAuthentication());

            // send
            var response = RavenCommandsRepository.GetTestStauses().Send(executor).AsJToken();

            // extract status
            var status = response.FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(outcome, Compare));

            // exit conditions
            if (status == default)
            {
                return;
            }

            // exit conditions
            if (!int.TryParse($"{status["id"]}", out int outcomeOut))
            {
                return;
            }

            // send
            RavenCommandsRepository.SetTestExecuteResult(testCase.TestRunKey, testCase.Key, outcomeOut).Send(executor);
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
            var runOut = 0;
            if (testCase.Context.ContainsKey("runtimeid"))
            {
                _ = int.TryParse($"{testCase.Context["runtimeid"]}", out runOut);
            }

            // build
            var executor = new JiraCommandsExecutor(testCase.GetAuthentication());

            // send
            foreach (var (Id, Data) in GetEvidence(testCase).Reverse())
            {
                RavenCommandsRepository.CreateAttachment($"{runOut}", $"{Id}", Data).Send(executor);
            }
        }

        private static IEnumerable<(long Id, IDictionary<string, object> Data)> GetEvidence(RhinoTestCase testCase)
        {
            // get for step
            var evidences = new ConcurrentBag<(long, IDictionary<string, object>)>();
            foreach (var step in testCase.Steps)
            {
                var isRuntime = long.TryParse($"{step.Context["runtimeid"]}", out long runtimeOut);
                var runtimeid = isRuntime ? runtimeOut : -1;

                foreach (var screenshot in step.GetScreenshots())
                {
                    var evidence = GetEvidenceBody(screenshot);
                    evidences.Add((runtimeid, evidence));
                }
            }

            // get
            return evidences;
        }

        private static IDictionary<string, object> GetEvidenceBody(string screenshot)
        {
            return new Dictionary<string, object>
            {
                ["filename"] = Path.GetFileName(screenshot),
                ["contentType"] = "image/png",
                ["data"] = Convert.ToBase64String(File.ReadAllBytes(screenshot))
            };
        }
        #endregion
    }
}