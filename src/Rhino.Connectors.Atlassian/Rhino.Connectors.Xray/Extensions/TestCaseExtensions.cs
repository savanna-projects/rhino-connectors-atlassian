/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Framework;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using RhinoUtilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestCaseExtensions
    {
        // members: constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

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
            var stepsCount = testCase.Steps.Count();

            // apply runtime id to test-step context
            for (int i = 0; i < stepsCount; i++)
            {
                testCase.Steps.ElementAt(i).Context["runtimeid"] = stepsToken[i]["id"].ToObject<long>();
                testCase.Steps.ElementAt(i).Context["testStep"] = JToken.Parse($"{stepsToken[i]}");
            }

            // apply test run key
            int.TryParse($"{response["id"]}", out int idOut);
            testCase.Context["runtimeid"] = idOut;
            testCase.Context["testRunKey"] = testExecutionKey;
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

        #region *** To Issue Request ***
        /// <summary>
        /// Converts connector test case interface into a test management test case.
        /// </summary>
        /// <param name="testCase">Test case to convert</param>
        /// <returns>Test management test case</returns>
        public static string ToJiraXrayIssue(this RhinoTestCase testCase)
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
                [$"{testCase.Context["manual-test-steps-custom-field"]}"] = new Dictionary<string, object>
                {
                    ["steps"] = steps
                }
            };

            // test suite
            if (testCase.TestSuites.Any())
            {
                payload[$"{testCase.Context["test-sets-custom-field"]}"] = testCase.TestSuites;
            }

            // test plan
            var testPlans = testCase.GetConnectorCapability(capability: AtlassianCapabilities.TestPlans, defaultValue: Array.Empty<string>());
            var isTestPlan = testPlans.Length != 0 && testCase.Context.ContainsKey("test-plan-custom-field");
            if (isTestPlan)
            {
                payload[$"{testCase.Context["test-plan-custom-field"]}"] = testPlans;
            }
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
            // get steps
            // add exceptions images - if exists or relevant
            if (testCase.Context.ContainsKey(ContextEntry.OrbitResponse))
            {
                testCase.AddExceptionsScreenshot();
            }

            // collect steps
            var steps = new List<object>();
            foreach (var testStep in testCase.Steps)
            {
                steps.Add(testStep.GetUpdateRequest(outcome: $"{testCase.Context["outcome"]}"));
            }

            // setup
            var executor = new JiraCommandsExecutor(testCase.GetAuthentication());
            var command = RavenCommandsRepository.UpdateTestRun(
                testRun: $"{testCase.Context["runtimeid"]}",
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
                int.TryParse($"{testCase.Context["runtimeid"]}", out runOut);
            }

            // build
            var executor = new JiraCommandsExecutor(testCase.GetAuthentication());

            // send
            foreach (var (Id, Data) in GetEvidence(testCase))
            {
                RavenCommandsRepository.CreateAttachment($"{runOut}", $"{Id}", Data).Send(executor);
            }
        }

        private static IEnumerable<(long Id, IDictionary<string, object> Data)> GetEvidence(RhinoTestCase testCase)
        {
            // get screenshots
            var screenshots = testCase.GetScreenshots();
            var automation = testCase.GetWebAutomation();

            // exit conditions
            if (!screenshots.Any() || automation == default)
            {
                return Array.Empty<(long, IDictionary<string, object>)>();
            }

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
                var reference = testCase.GetActionReference(referenceOut).Reference;
                var evidence = GetEvidenceBody(screenshot);
                var runtimeid = testCase.Steps.ElementAt(reference).Context.ContainsKey("runtimeid")
                    ? (long)testCase.Steps.ElementAt(reference).Context["runtimeid"]
                    : -1;
                evidences.Add((runtimeid, evidence));
            }

            // results
            return evidences;
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
        #endregion

        #region *** Update: Bug      ***
        /// <summary>
        /// Updates a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update a bug.</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> if not.</returns>
        public static bool UpdateBug(this RhinoTestCase testCase, string idOrKey, JiraClient jiraClient)
        {
            // setup
            var bugType = testCase.GetConnectorCapability(capability: AtlassianCapabilities.BugType, defaultValue: "Bug");
            var onBug = jiraClient.Get(idOrKey);

            // setup conditions
            var isDefault = onBug == default;
            var isBug = !isDefault && $"{onBug.SelectToken("fields.issuetype.name")}".Equals(bugType, Compare);

            // exit conditions
            if (!isBug)
            {
                return false;
            }

            // update body
            var requestBody = GetUpdateBugPayload(testCase, onBug, jiraClient);
            var isUpdate = jiraClient.UpdateIssue(idOrKey, requestBody);
            if (!isUpdate)
            {
                return isUpdate;
            }

            // delete all attachments
            jiraClient.DeleteAttachments(idOrKey: $"{onBug["key"]}");

            // upload new attachments
            var files = testCase.GetScreenshots();
            jiraClient.AddAttachments($"{onBug["key"]}", files.ToArray());

            // results
            return isUpdate;
        }

        private static object GetUpdateBugPayload(RhinoTestCase testCase, JToken onBug, JiraClient jiraClient)
        {
            // setup
            var comment =
                $"{RhinoUtilities.GetActionSignature("updated")} " +
                $"Bug status on execution [{testCase.TestRunKey}] is *{onBug.SelectToken("fields.status.name")}*.";

            // verify if bug is already open
            var template = testCase.BugMarkdown(jiraClient);
            var description = $"{JToken.Parse(template).SelectToken("fields.description")}";

            // setup
            return new
            {
                Update = new
                {
                    Comment = new[]
                    {
                        new
                        {
                            Add = new
                            {
                                Body = comment
                            }
                        }
                    }
                },
                Fields = new
                {
                    Description = description
                }
            };
        }
        #endregion

        #region *** Create: Bug      ***
        /// <summary>
        /// Creates a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to create a bug.</param>
        /// <returns>Bug creation results from Jira.</returns>
        public static JToken CreateBug(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            // setup
            var issueBody = testCase.BugMarkdown(jiraClient);

            // post
            var response = jiraClient.Create(issueBody);
            if (response == default)
            {
                return default;
            }

            // link to test case
            var comment =
                $"{RhinoUtilities.GetActionSignature("created")} " +
                $"On execution [{testCase.TestRunKey}]";
            jiraClient.CreateIssueLink(linkType: "Blocks", inward: $"{response["key"]}", outward: testCase.Key, comment);

            // add attachments
            var files = testCase.GetScreenshots();
            jiraClient.AddAttachments($"{response["key"]}", files.ToArray());

            // add to context
            testCase.Context["bugOpenedResponse"] = response;

            // results
            return response;
        }
        #endregion

        #region *** Close: Bug       ***
        /// <summary>
        /// Close a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to close a bug.</param>
        /// <param name="bugIssueKey">The bug issue key to close.</param>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static bool CloseBug(this RhinoTestCase testCase, string bugIssueKey, string resolution, JiraClient jiraClient)
        {
            // get existing bugs
            var isBugs = testCase.Context.ContainsKey("bugs") && testCase.Context["bugs"] != default;
            var bugs = isBugs ? (IEnumerable<string>)testCase.Context["bugs"] : Array.Empty<string>();
            var exists = bugs.Any(i => i.Equals(bugIssueKey, Compare));

            // exit conditions
            if (!exists)
            {
                return false;
            }

            // setup
            var transitions = jiraClient.GetTransitions(bugIssueKey);
            var comment = $"{RhinoUtilities.GetActionSignature("closed")} On execution [{testCase.TestRunKey}]";

            // exit conditions
            if (!transitions.Any())
            {
                return false;
            }

            // get transition
            var transition = transitions.FirstOrDefault(i => i["to"].Equals("Closed", Compare));
            if (transition == default)
            {
                return false;
            }

            //send transition
            return jiraClient.CreateTransition(
                idOrKey: bugIssueKey,
                transitionId: transition["id"],
                resolution: resolution,
                comment);
        }
        #endregion

        #region *** Bug/Test Match   ***
        /// <summary>
        /// Return true if a bug meta data match to test meta data.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to match to.</param>
        /// <param name="bug">Bug JSON token to match by.</param>
        /// <param name="assertDataSource"><see cref="true"/> to match also RhinoTestCase.DataSource</param>
        /// <returns><see cref="true"/> if match, <see cref="false"/> if not.</returns>
        public static bool IsBugMatch(this RhinoTestCase testCase, JToken bug, bool assertDataSource)
        {
            // setup
            var onBug = $"{bug}";
            var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

            // build fields
            int.TryParse(Regex.Match(input: onBug, pattern: @"(?<=\WOn Iteration\W+)\d+").Value, out int iteration);
            var driver = Regex.Match(input: onBug, pattern: @"(?<=\|Driver\|)\w+(?=\|)").Value;

            // setup conditions
            var isCapabilities = AssertCapabilities(testCase, onBug);
            var isDataSource = AssertDataSource(testCase, onBug);
            var isDriver = $"{driverParams["driver"]}".Equals(driver, Compare);
            var isIteration = testCase.Iteration == iteration;
            var isOptions = AssertOptions(testCase, onBug);

            // assert
            return assertDataSource
                ? isDataSource && isCapabilities && isDriver && isIteration && isOptions
                : isCapabilities && isDriver && isIteration && isOptions;
        }

        private static bool AssertCapabilities(RhinoTestCase testCase, string onBug)
        {
            try
            {
                // setup
                var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

                // extract test capabilities
                var tstCapabilities = driverParams.ContainsKey("capabilities")
                    ? ((IDictionary<string, object>)driverParams["capabilities"]).ToJiraMarkdown()
                    : string.Empty;

                // normalize to markdown
                var onTstCapabilities = Regex.Split(string.IsNullOrEmpty(tstCapabilities) ? string.Empty : tstCapabilities, @"\\r\\n");
                tstCapabilities = string.Join(Environment.NewLine, onTstCapabilities);

                // extract bug capabilities
                var bugCapabilities = Regex.Match(
                    input: onBug,
                    pattern: @"(?<=Capabilities\W+\\r\\n\|\|).*(?=\|.*Local Data Source)|(?<=Capabilities\W+\\r\\n\|\|).*(?=\|)").Value;

                // normalize to markdown
                var onBugCapabilities = Regex.Split(string.IsNullOrEmpty(bugCapabilities) ? string.Empty : "||" + bugCapabilities + "|", @"\\r\\n");
                bugCapabilities = string.Join(Environment.NewLine, onBugCapabilities);

                // exit conditions
                var isBugCapabilities = !string.IsNullOrEmpty(bugCapabilities);
                var isTstCapabilities = !string.IsNullOrEmpty(tstCapabilities);
                if (isBugCapabilities ^ isTstCapabilities)
                {
                    return false;
                }
                else if (!isBugCapabilities && !isTstCapabilities)
                {
                    return true;
                }

                // convert to data table and than to dictionary collection
                var compareableBugCapabilites = new DataTable().FromMarkDown(bugCapabilities).ToDictionary().ToJson().ToUpper().Sort();
                var compareableTstCapabilites = new DataTable().FromMarkDown(tstCapabilities).ToDictionary().ToJson().ToUpper().Sort();

                // assert
                return compareableBugCapabilites.Equals(compareableTstCapabilites, Compare);
            }
            catch (Exception e) when (e != null)
            {
                return false;
            }
        }

        private static bool AssertDataSource(RhinoTestCase testCase, string onBug)
        {
            try
            {
                // extract test capabilities
                var compareableTstData = testCase.DataSource?.Any() == true
                    ? testCase.DataSource.ToJson().ToUpper().Sort()
                    : string.Empty;

                // extract bug capabilities
                var bugData = Regex.Match(input: onBug, pattern: @"(?<=Local Data Source\W+\\r\\n\|\|).*(?=\|)").Value;

                // normalize to markdown
                var onBugData = Regex.Split(string.IsNullOrEmpty(bugData) ? string.Empty : "||" + bugData + "|", @"\\r\\n");
                bugData = string.Join(Environment.NewLine, onBugData);

                // exit conditions
                var isBugCapabilities = !string.IsNullOrEmpty(compareableTstData);
                var isTstCapabilities = !string.IsNullOrEmpty(bugData);
                if (isBugCapabilities ^ isTstCapabilities)
                {
                    return false;
                }
                else if (!isBugCapabilities && !isTstCapabilities)
                {
                    return true;
                }

                // convert to data table and than to dictionary collection
                var compareableBugCapabilites = new DataTable()
                    .FromMarkDown(bugData)
                    .ToDictionary()
                    .ToJson()
                    .ToUpper()
                    .Sort();

                // assert
                return compareableBugCapabilites.Equals(compareableTstData, Compare);
            }
            catch (Exception e) when (e != null)
            {
                return false;
            }
        }

        private static bool AssertOptions(RhinoTestCase testCase, string onBug)
        {
            // setup
            var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

            // extract test capabilities
            var tstOptions = driverParams.ContainsKey("options")
                ? JsonConvert.SerializeObject(driverParams["options"], Formatting.None).ToUpper().Sort()
                : string.Empty;

            // extract bug capabilities
            var onBugOptions = Regex.Match(input: onBug, pattern: @"(?<=Options\W+\\r\\n\{code:json}).*?(?=\{code})").Value;
            onBugOptions = onBugOptions.Replace("\\r", string.Empty).Replace("\\n", string.Empty).Replace(@"\", string.Empty);
            var bugOptions = string.IsNullOrEmpty(onBugOptions) ? string.Empty : onBugOptions;

            // deserialize
            if (!string.IsNullOrEmpty(bugOptions))
            {
                var bugOptionsObjt = JsonConvert.DeserializeObject<object>(bugOptions);
                bugOptions = JsonConvert.SerializeObject(bugOptionsObjt, Formatting.None).ToUpper().Sort();
            }

            // assert
            return tstOptions.Equals(bugOptions, Compare);
        }
        #endregion
    }
}