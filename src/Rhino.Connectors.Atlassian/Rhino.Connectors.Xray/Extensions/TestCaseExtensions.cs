/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;
using Gravity.Services.DataContracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Contracts.Interfaces;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.Xray.Contracts;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using RhinoUtilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class TestCaseExtensions
    {
        // members: constants
        private const string RavenExecutionFormat = "/rest/raven/2.0/api/testrun/?testExecIssueKey={0}&testIssueKey={1}";
        private const string RavenRunFormat = "/rest/raven/2.0/api/testrun/{0}";
        private const string RavenAttachmentFormat = "/rest/raven/2.0/api/testrun/{0}/step/{1}/attachment";

        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

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
            var requestBody = JsonConvert.SerializeObject(new { Comment = comment }, JiraClient.JsonSettings);
            var content = new StringContent(requestBody, Encoding.UTF8, JiraClient.MediaType);

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

        /// <summary>
        /// Gets a value from the capabilities dictionary under the ProviderConfiguration of this RhinoTestCase.
        /// </summary>
        /// <typeparam name="T">The capability type to return.</typeparam>
        /// <param name="onContext">Context dictionary from which to get the capability.</param>
        /// <param name="capability">The capability to get.</param>
        /// <param name="defaultValue">The default value to get if the capability was not found.</param>
        /// <returns>Capability value.</returns>
        public static T GetCapability<T>(this IHasContext onContext, string capability, T defaultValue = default)
        {
            return DoGetCapability(onContext, capability, defaultValue);
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
            if (!string.IsNullOrEmpty(testCase.TestSuite))
            {
                payload[$"{testCase.Context["test-sets-custom-field"]}"] = new[] { testCase.TestSuite };
            }

            // test plan
            var testPlans = DoGetCapability(onContext: testCase, capability: XrayCapabilities.TestPlans, defaultValue: Array.Empty<string>());
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
        public static int SetOutcome(this RhinoTestCase testCase)
        {
            // get request content
            var request = new
            {
                steps = GetUpdateRequestObject(testCase)
            };
            var requestBody = JsonConvert.SerializeObject(request, JiraClient.JsonSettings);
            var content = new StringContent(requestBody, Encoding.UTF8, JiraClient.MediaType);

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

        private static List<object> GetUpdateRequestObject(RhinoTestCase testCase)
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
                steps.Add(testStep.GetUpdateRequest());
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

        #region *** Bug Payload      ***
        /// <summary>
        /// Updates a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update a bug.</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> if not.</returns>
        public static bool UpdateBug(this RhinoTestCase testCase, JiraClient jiraClient, string issueKey)
        {
            // setup
            var bugType = DoGetCapability<string>(testCase, capability: XrayCapabilities.BugType, "Bug");
            var onBug = jiraClient.GetIssue(issueKey);

            // setup conditions
            var isDefault = onBug == default;
            var isBug = !isDefault && $"{onBug.SelectToken("fields.issuetype.name")}".Equals(bugType, Compare);

            // exit conditions
            if (!isBug)
            {
                return false;
            }

            var requestBody = GetUpdateBugPayload(testCase, onBug, jiraClient);
            return jiraClient.UpdateIssue(requestBody, issueKey);
        }

        private static string GetUpdateBugPayload(RhinoTestCase testCase, JObject onBug, JiraClient jiraClient)
        {
            // setup
            var comment =
                $"{RhinoUtilities.GetActionSignature("updated")} " +
                $"Bug status on execution [{testCase.TestRunKey}] is *{onBug.SelectToken("fields.status.name")}*.";

            // verify if bug is already open
            var description = $"{JObject.Parse(GetBugRequestTemplate(testCase, jiraClient)).SelectToken("fields.description")}";

            // setup
            var payload = new
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
            return JsonConvert.SerializeObject(payload, JiraClient.JsonSettings);
        }

        /// <summary>
        /// Creates a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to create a bug.</param>
        /// <returns>Bug creation results from Jira.</returns>
        public static JObject CreateBug(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            // setup
            var issueBody = GetBugRequestTemplate(testCase, jiraClient);

            // post
            var response = jiraClient.CreateIssue(issueBody);
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
            var files = GetScreenshots(testCase).ToArray();
            jiraClient.AddAttachments($"{response["key"]}", files);

            // add to context
            testCase.Context["bug"] = response;

            // results
            return response;
        }

        private static string GetBugRequestTemplate(RhinoTestCase testCase, JiraClient jiraClient)
        {
            // load JSON body
            return Assembly.GetExecutingAssembly().ReadEmbeddedResource("create_bug_for_test_jira.txt")
                .Replace("[project-key]", $"{testCase.Context["projectKey"]}")
                .Replace("[test-scenario]", testCase.Scenario)
                .Replace("[test-priority]", GetPriority(testCase, jiraClient))
                .Replace("[test-actions]", GetDescriptionMarkdown(testCase))
                .Replace("[test-environment]", GetEnvironmentMarkdown(testCase))
                .Replace("[test-id]", testCase.Key);
        }

        private static string GetPriority(RhinoTestCase testCase, JiraClient jiraClient)
        {
            // get priority token
            var priorityData = jiraClient.GetIssueTypeFields("Bug", "fields.priority");

            // exit conditions
            if (string.IsNullOrEmpty(priorityData))
            {
                return string.Empty;
            }

            // setup
            var id = Regex.Match(input: testCase.Priority, @"\d+").Value;
            var name = Regex.Match(input: testCase.Priority, @"(?<=\d+\s+-\s+)\w+").Value;

            // extract
            var priority = JObject
                .Parse(priorityData)["allowedValues"]
                .FirstOrDefault(i => $"{i["name"]}".Equals(name, Compare) && $"{i["id"]}".Equals(id, Compare));

            // results
            return $"{priority["id"]}";
        }

        private static string GetEnvironmentMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // setup
                var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

                // setup conditions
                var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;
                var isCapabilites = driverParams.ContainsKey("capabilities");
                var isMobApp = !isWebApp
                    && isCapabilites
                    && ((IDictionary<string, object>)driverParams["capabilities"]).ContainsKey("app");

                // get application
                return isMobApp
                    ? $"{((IDictionary<string, object>)driverParams["capabilities"])["app"]}"
                    : ((ActionRule)testCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument;
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        private static string GetDescriptionMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // set header
                var header =
                    "\\r\\n----\\r\\n" +
                    "*Last Update: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC*\\r\\n" +
                    "*On Iteration*: " + $"{testCase.Iteration}\\r\\n" +
                    "Bug filed on '" + testCase.Scenario + "'\\r\\n" +
                    "----\\r\\n";

                // set steps
                var steps = string.Join("\\r\\n\\r\\n", testCase.Steps.Select(GetStepMarkdown));

                // results
                return header + steps + GetPlatformMarkdown(testCase);
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        private static string GetStepMarkdown(RhinoTestStep testStep)
        {
            // setup action
            var action = "*" + testStep.Action.Replace("{", "\\\\{") + "*\\r\\n";

            // setup
            var expectedResults = Regex
                .Split(testStep.Expected, "(\r\n|\r|\n)")
                .Where(i => !string.IsNullOrEmpty(i) && !Regex.IsMatch(i, "(\r\n|\r|\n)"))
                .ToArray();

            var failedOn = testStep.Context.ContainsKey(ContextEntry.FailedOn)
                ? (IEnumerable<int>)testStep.Context[ContextEntry.FailedOn]
                : Array.Empty<int>();

            // exit conditions
            if (!failedOn.Any())
            {
                return action;
            }

            // build
            var markdown = action + "||Result||Assertion||\\r\\n";
            for (int i = 0; i < expectedResults.Length; i++)
            {
                var outcome = failedOn.Contains(i) ? "(x)" : "(/)";
                markdown += "|" + outcome + "|" + expectedResults[i].Replace("{", "\\\\{") + "|\\r\\n";
            }

            // results
            return markdown.Trim();
        }

        private static string GetPlatformMarkdown(RhinoTestCase testCase)
        {
            // setup
            var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

            // set header
            var header =
                "\\r\\n----\\r\\n" +
                "*On Platform*: " + $"{driverParams["driver"]}\\r\\n" +
                "----\\r\\n";

            // setup conditions
            var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;
            var isCapabilites = driverParams.ContainsKey("capabilities");
            var isMobApp = !isWebApp
                && isCapabilites
                && ((IDictionary<string, object>)driverParams["capabilities"]).ContainsKey("app");

            // get application
            var application = isMobApp
                ? ((IDictionary<string, object>)driverParams["capabilities"])["app"]
                : ((ActionRule)testCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument;

            // setup environment
            var environment =
                "*Application Under Test*\\r\\n" +
                "||Name||Value||\\r\\n" +
                "|Driver|" + $"{driverParams["driver"]}" + "|\\r\\n" +
                "|Driver Server|" + $"{driverParams["driverBinaries"]}".Replace(@"\", @"\\") + "|\\r\\n" +
                "|Application|" + application + "|\\r\\n";

            var capabilites = isCapabilites
                ? "*Capabilities*\\r\\n" + ((IDictionary<string, object>)driverParams["capabilities"]).ToXrayMarkdown() + "\\r\\n"
                : string.Empty;

            var dataSource = testCase.DataSource.Any()
                ? "*Local Data Source*\\r\\n" + testCase.DataSource.ToXrayMarkdown()
                : string.Empty;

            // results
            return (header + environment + capabilites + dataSource).Trim();
        }

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
                return true;
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
            return jiraClient.Transition(
                issueKey: bugIssueKey,
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
        /// <returns><see cref="true"/> if match, <see cref="false"/> if not.</returns>
        public static bool IsBugMatch(this RhinoTestCase testCase, JObject bug)
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

            // assert
            return isCapabilities && isDataSource && isDriver && isIteration;
        }

        private static bool AssertCapabilities(RhinoTestCase testCase, string onBug)
        {
            // setup
            var driverParams = (IDictionary<string, object>)testCase.Context[ContextEntry.DriverParams];

            // extract test capabilities
            var tstCapabilities = driverParams.ContainsKey("capabilities")
                ? ((IDictionary<string, object>)driverParams["capabilities"]).ToXrayMarkdown()
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

            // convert to data table and than to dictionary collection
            var compareableBugCapabilites = new DataTable().FromMarkDown(bugCapabilities).ToDictionary().ToJson().ToUpper().Sort();
            var compareableTstCapabilites = new DataTable().FromMarkDown(tstCapabilities).ToDictionary().ToJson().ToUpper().Sort();

            // assert
            return compareableBugCapabilites.Equals(compareableTstCapabilites, Compare);
        }

        private static bool AssertDataSource(RhinoTestCase testCase, string onBug)
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
                var content = new StringContent(JsonConvert.SerializeObject(Data), Encoding.UTF8, JiraClient.MediaType);
                JiraClient.HttpClient.PostAsync(route, content).GetAwaiter().GetResult();
            }
        }

        private static IEnumerable<(long Id, IDictionary<string, object> Data)> GetEvidence(RhinoTestCase testCase)
        {
            // get screenshots
            var screenshots = GetScreenshots(testCase);
            var automation = GetWebAutomation(testCase);

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
                var reference = GetActionReference(testCase, referenceOut).Reference;
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
                .AppendLine(JsonConvert.SerializeObject(testCase.Context[ContextEntry.DriverParams], JiraClient.JsonSettings))
                .AppendLine()
                .AppendLine("[Local Data Source]")
                .Append(JsonConvert.SerializeObject(testCase.DataSource, JiraClient.JsonSettings))
                .AppendLine("{noformat}");

            // return
            return comment.ToString();
        }

        private static IEnumerable<string> GetScreenshots(RhinoTestCase testCase)
        {
            // exit conditions
            if (!testCase.Context.ContainsKey(ContextEntry.OrbitResponse))
            {
                return Array.Empty<string>();
            }

            // get
            return ((OrbitResponse)testCase.Context[ContextEntry.OrbitResponse])
                .OrbitRequest
                .Screenshots
                .Select(i => i.Location);
        }

        private static WebAutomation GetWebAutomation(RhinoTestCase testCase)
        {
            // exit conditions
            if (!testCase.Context.ContainsKey(ContextEntry.WebAutomation))
            {
                return default;
            }

            // get
            return (WebAutomation)testCase.Context[ContextEntry.WebAutomation];
        }

        // gets a capability value from test case configuration or default value if not possible
        private static T DoGetCapability<T>(IHasContext onContext, string capability, T defaultValue = default)
        {
            try
            {
                // setup
                var isKey = onContext.Context.ContainsKey(ContextEntry.Configuration);
                var isValue = isKey && onContext.Context[ContextEntry.Configuration] != default;

                // exit conditions
                if (!isValue)
                {
                    return defaultValue;
                }

                // setup
                var configuration = ((RhinoConfiguration)onContext.Context[ContextEntry.Configuration]).ProviderConfiguration;
                var isNotNull = configuration?.Capabilities != default;
                isKey = isNotNull && configuration.Capabilities.ContainsKey(capability);
                isValue = isKey && !string.IsNullOrEmpty($"{configuration.Capabilities[capability]}");

                // results
                return isValue ? (T)configuration.Capabilities[capability] : defaultValue;
            }
            catch (Exception e) when (e != null)
            {
                return defaultValue;
            }
        }

        // gets the first action reference which is not "Assert" in a given action rules collection
        // and starting reference
        private static (string Command, int Reference) GetActionReference(RhinoTestCase testCase, int reference)
        {
            // flatten
            var actions = new List<(string Command, int Reference)>();
            for (int i = 0; i < testCase.Steps.Count(); i++)
            {
                // setup
                var onStep = testCase.Steps.ElementAt(i);
                var assertions = onStep.Expected
                    .SplitByLines()
                    .Select(i => i.Trim())
                    .Where(i => !string.IsNullOrEmpty(i))
                    .Select(_ => (ActionType.Assert, i));

                actions.Add((onStep.Command, i));
                actions.AddRange(assertions);
            }

            // exit conditions
            if (actions[reference].Command != ActionType.Assert)
            {
                return actions[reference];
            }

            // recurse
            return GetActionReference(testCase, reference - 1);
        }
    }
}