﻿/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Extensions;
using Rhino.Connectors.Xray.Framework;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Utilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray
{
    /// <summary>
    /// XRay connector for using XRay tests as Rhino Specs.
    /// </summary>
    public class XrayAutomationProvider : ProviderManager
    {
        // members: constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        // state: global parameters
        private readonly ILogger logger;
        private readonly IDictionary<string, object> capabilities;
        private readonly JiraClient jiraClient;
        private readonly JiraCommandsExecutor ravenClient;

        #region *** Public Constants  ***
        public const string TestPlanSchema = "com.xpandit.plugins.xray:tests-associated-with-test-plan-custom-field";
        public const string TestSetTestsSchema = "com.xpandit.plugins.xray:test-sets-tests-custom-field";
        public const string TestSetSchema = "com.xpandit.plugins.xray:test-sets-custom-field";
        public const string TestCaseSchema = "com.xpandit.plugins.xray:test-sets-custom-field";
        public const string TestExecutionSchema = "com.xpandit.plugins.xray:testexec-tests-custom-field";
        public const string PreconditionSchema = "com.xpandit.plugins.xray:test-precondition-custom-field";
        public const string ManualTestStepSchema = "com.xpandit.plugins.xray:manual-test-steps-custom-field";
        public const string AssociatedPlanSchema = "com.xpandit.plugins.xray:test-plans-associated-with-test-custom-field";
        #endregion

        #region *** Constructors      ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        public XrayAutomationProvider(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this provider.</param>
        public XrayAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
            : base(configuration, types, logger)
        {
            // setup
            this.logger = logger?.Setup(loggerName: nameof(XrayAutomationProvider));

            var authentication = configuration.GetJiraAuthentication();
            jiraClient = new JiraClient(authentication);
            ravenClient = new JiraCommandsExecutor(authentication);

            // capabilities
            BucketSize = configuration.GetBuketSize();
            configuration.PutIssueTypes();
            capabilities = configuration.Capabilities.ContainsKey($"{Connector.JiraXRay}:options")
                ? configuration.Capabilities[$"{Connector.JiraXRay}:options"] as IDictionary<string, object>
                : new Dictionary<string, object>();
        }
        #endregion        

        #region *** Get: Test Cases   ***
        /// <summary>
        /// Returns a list of test cases for a project.
        /// </summary>
        /// <param name="ids">A list of issue id or key to get test cases by.</param>
        /// <returns>A collection of Rhino.Api.Contracts.AutomationProvider.RhinoTestCase</returns>
        public override IEnumerable<RhinoTestCase> OnGetTestCases(params string[] ids)
        {
            // setup
            var testCases = new ConcurrentBag<RhinoTestCase>();

            // iterate - one by one on debug, parallel on production
            foreach (var issueKeys in ids.Split(BucketSize))
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = BucketSize
                };
                Parallel.ForEach(issueKeys, options, key => testCases.AddRange(GetTests(key)));
            }

            // get
            return testCases;
        }

        private IEnumerable<RhinoTestCase> GetTests(string issueKey)
        {
            // get issue type
            var issueType = jiraClient.GetIssueType(issueKey);
            var capability = string.Empty;
            var typeEntry = capabilities.Where(i => $"{i.Value}".Equals(issueType, Compare));
            if (typeEntry.Any())
            {
                capability = $"{typeEntry.ElementAt(0).Key}";
            }

            // get fetching method
            var method = GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(i => i.GetCustomAttribute<DescriptionAttribute>() != null)
                .FirstOrDefault(i => i.GetCustomAttribute<DescriptionAttribute>().Description.Equals(capability, Compare));

            // exit conditions
            if (method == default)
            {
                logger?.Error($"Get-Tests -By [{issueType}] = false");
                return Array.Empty<RhinoTestCase>();
            }

            // invoke and return results
            return method.Invoke(this, new object[] { issueKey }) as IEnumerable<RhinoTestCase>;
        }

        // process test cases & test sets based on associated test set 
        [Description(AtlassianCapabilities.PlanType)]
        private IEnumerable<RhinoTestCase> GetByPlan(string issueKey)
        {
            // parse into JToken
            var jsonObject = jiraClient.Get(issueKey);
            if (jsonObject == default)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // find & validate test cases
            var customField = jiraClient.GetCustomField(TestPlanSchema);
            var onTestCases = jsonObject.SelectToken($"..{customField}");
            Logger?.DebugFormat($"Get-Tests -By [{AtlassianCapabilities.PlanType}] = {onTestCases.Count()}");

            // iterate & load tests
            var testCases = new List<RhinoTestCase>();
            foreach (var onTestCase in onTestCases)
            {
                testCases.AddRange(GetOne($"{onTestCase}"));
            }
            return testCases;
        }

        private IEnumerable<RhinoTestCase> GetOne(string issueKey)
        {
            // get issue & exit conditions
            var JToken = jiraClient.Get(issueKey);
            if (JToken == default)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // extract issue type
            var type = jiraClient.GetIssueType(issueKey);

            // setup conditions & exit conditions
            var isTest = type.Equals($"{Configuration.Capabilities[AtlassianCapabilities.TestType]}", Compare);
            var isTestSet = type.Equals($"{Configuration.Capabilities[AtlassianCapabilities.SetType]}", Compare);
            if (!isTest && !isTestSet)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // return conditions
            return isTest ? new[] { DoGetByTest(issueKey) } : DoGetBySet(issueKey);
        }

        // process test cases based on associated test set
        [Description(AtlassianCapabilities.SetType)]
        private IEnumerable<RhinoTestCase> GetBySet(string issueKey)
        {
            return DoGetBySet(issueKey);
        }

        // process test cases based on associated test set
        [Description(AtlassianCapabilities.ExecutionType)]
        private IEnumerable<RhinoTestCase> GetByExecution(string issueKey)
        {
            // parse into JToken
            var jsonObject = jiraClient.Get(issueKey);
            if (jsonObject == default)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // find & validate test cases
            var customField = jiraClient.GetCustomField(TestExecutionSchema);
            var onTestCases = jsonObject.SelectToken($"..{customField}");
            Logger?.DebugFormat($"Get-Tests -By [{AtlassianCapabilities.ExecutionType}] = {onTestCases.Count()}");

            // parse into connector test case
            var testCases = new List<RhinoTestCase>();
            foreach (var onTestCase in onTestCases.Children())
            {
                testCases.Add(DoGetByTest($"{onTestCase["b"]}"));
            }
            Configuration.TestsRepository = testCases.Select(i => i.Key).Distinct();
            return testCases;
        }

        // process a single test
        [Description(AtlassianCapabilities.TestType)]
        private IEnumerable<RhinoTestCase> GetByTest(string issueKey)
        {
            // setup
            var testCase = DoGetByTest(issueKey);

            // results
            return new[] { testCase };
        }

        // COMMON METHODS
        private IEnumerable<RhinoTestCase> DoGetBySet(string issueKey)
        {
            // parse into JToken
            var jsonObject = jiraClient.Get(issueKey);
            if (jsonObject == default)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // find & validate test cases
            var customField = jiraClient.GetCustomField(TestSetTestsSchema);
            var onTestCases = jsonObject.SelectToken($"..{customField}");
            Logger?.DebugFormat($"Get-Tests -By [{AtlassianCapabilities.SetType}] = {onTestCases.Count()}");

            // parse into connector test case
            var testCases = new ConcurrentBag<RhinoTestCase>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };
            Parallel.ForEach(onTestCases.Children(), options, onTestCase => testCases.Add(DoGetByTest($"{onTestCase}")));

            // get
            return testCases;
        }

        private RhinoTestCase DoGetByTest(string issueKey)
        {
            // parse into JToken
            var jsonObject = jiraClient.Get(issueKey);

            // parse into connector test case
            var test = jsonObject == default ? new RhinoTestCase { Key = "-1" } : jsonObject.ToRhinoTestCase();
            if (test.Key.Equals("-1"))
            {
                return test;
            }

            // setup project key
            test.Context["projectKey"] = $"{jsonObject.SelectToken("fields.project.key")}";

            // load test set (if available - will take the )
            var customField = jiraClient.GetCustomField(TestCaseSchema);
            var testSets = jsonObject.SelectToken($"..{customField}");
            if (testSets.Any())
            {
                test.TestSuites = testSets.Select(i => $"{i}");
            }

            // load test-plans if any
            customField = jiraClient.GetCustomField(AssociatedPlanSchema);
            var testPlans = jsonObject.SelectToken($"..{customField}");
            var onTestPlans = new List<string>();
            foreach (var testPlan in testPlans)
            {
                onTestPlans.Add($"{testPlan}");
            }
            test.Context["testPlans"] = onTestPlans.Count > 0 ? onTestPlans : new List<string>();

            // load data-sources (multiple preconditions data loading)
            customField = jiraClient.GetCustomField(PreconditionSchema);
            var preconditions = jsonObject.SelectToken($"..{customField}");
            if (!preconditions.Any())
            {
                return test;
            }

            // load preconditions
            var mergedDataSource = preconditions
                .Select(i => new DataTable().FromMarkDown($"{jiraClient.Get($"{i}").SelectToken("fields.description")}".Trim(), default))
                .Merge();
            test.DataSource = mergedDataSource.ToDictionary().Cast<Dictionary<string, object>>().ToArray();

            // return populated test
            return test;
        }
        #endregion

        #region *** Create: Test Case ***
        /// <summary>
        /// Creates a new test case under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider test case.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string CreateTestCase(RhinoTestCase testCase)
        {
            // shortcuts
            var onProject = Configuration.ConnectorConfiguration.Project;
            testCase.Context[ContextEntry.Configuration] = Configuration;
            var testType = $"{testCase.GetConnectorCapability(AtlassianCapabilities.TestType, "Test")}";

            // setup context
            testCase.Context["issuetype-id"] = $"{jiraClient.GetIssueTypeFields(idOrKey: testType, path: "id")}";
            testCase.Context["project-key"] = onProject;
            testCase.Context["test-sets-custom-field"] = jiraClient.GetCustomField(schema: TestSetSchema);
            testCase.Context["manual-test-steps-custom-field"] = jiraClient.GetCustomField(schema: ManualTestStepSchema);
            testCase.Context["test-plan-custom-field"] = jiraClient.GetCustomField(schema: AssociatedPlanSchema);

            // setup request body
            var requestBody = testCase.ToJiraXrayIssue();
            var issue = jiraClient.Create(requestBody);

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.CreateComment(idOrKey: $"{issue.SelectToken("key")}", comment);

            // success
            Logger?.InfoFormat($"Create-Test -Project [{onProject}] -Set [{string.Join(",", testCase?.TestSuites)}] = true");

            // results
            return $"{issue}";
        }
        #endregion

        #region *** Create: Test Run  ***
        /// <summary>
        /// Creates an automation provider test run entity. Use this method to implement the automation
        /// provider test run creation and to modify the loaded Rhino.Api.Contracts.AutomationProvider.RhinoTestRun.
        /// </summary>
        /// <param name="testRun">Rhino.Api.Contracts.AutomationProvider.RhinoTestRun object to modify before creating.</param>
        /// <returns>Rhino.Api.Contracts.AutomationProvider.RhinoTestRun based on provided test cases.</returns>
        public override RhinoTestRun OnCreateTestRun(RhinoTestRun testRun)
        {
            // exit conditions
            if (Configuration.IsDryRun())
            {
                testRun.Context["runtimeid"] = "-1";
                return testRun;
            }

            // setup: request body
            var customField = jiraClient.GetCustomField(TestExecutionSchema);
            var testCases = JsonConvert.SerializeObject(testRun.TestCases.Select(i => i.Key));

            // load JSON body
            var requestBody = Assembly.GetExecutingAssembly().ReadEmbeddedResource("create_test_execution_xray.txt")
                .Replace("[project-key]", Configuration.ConnectorConfiguration.Project)
                .Replace("[run-title]", TestRun.Title)
                .Replace("[custom-1]", customField)
                .Replace("[tests-repository]", testCases)
                .Replace("[type-name]", $"{capabilities[AtlassianCapabilities.ExecutionType]}")
                .Replace("[assignee]", Configuration.ConnectorConfiguration.User);
            var responseBody = jiraClient.Create(requestBody);

            // setup
            testRun.Key = $"{responseBody["key"]}";
            testRun.Link = $"{responseBody["self"]}";
            testRun.Context["runtimeid"] = $"{responseBody["id"]}";
            testRun.Context[ContextEntry.Configuration] = Configuration;

            // test steps handler
            foreach (var testCase in TestRun.TestCases)
            {
                testCase.SetRuntimeKeys(testRun.Key);
            }
            return testRun;
        }

        /// <summary>
        /// Completes automation provider test run results, if any were missed or bypassed.
        /// </summary>
        /// <param name="testRun">Rhino.Api.Contracts.AutomationProvider.RhinoTestRun results object to complete by.</param>
        public override void CompleteTestRun(RhinoTestRun testRun)
        {
            // exit conditions
            if (Configuration.IsDryRun())
            {
                return;
            }

            // setup: failed to update
            var inStatus = new[] { "TODO", "EXECUTING" };

            // get all test keys to re-assign outcome
            var testResults = testRun
                .GetTests()
                .Where(i => inStatus.Contains($"{i["status"]}"))
                .Select(i => $"{i["key"]}");

            // iterate: pass/fail
            foreach (var testCase in testRun.TestCases.Where(i => testResults.Contains(i.Key) && !i.Inconclusive))
            {
                DoUpdateTestResult(testCase, inline: false);
            }
            // iterate: align all runs
            var dataTests = testRun.TestCases.Where(i => i.Iteration > 0).Select(i => i.Key).Distinct();
            var options = new ParallelOptions { MaxDegreeOfParallelism = 1 };
            Parallel.ForEach(dataTests, options, testCase =>
            {
                var outcome = testRun.TestCases.Any(i => i.Key.Equals(testCase) && !i.Actual) ? "FAIL" : "PASS";
                if (outcome == "FAIL")
                {
                    testRun.TestCases.FirstOrDefault(i => !i.Actual && i.Key.Equals(testCase))?.SetOutcomeByRun(outcome);
                    return;
                }
                testRun.TestCases.FirstOrDefault(i => i.Actual && i.Key.Equals(testCase))?.SetOutcomeByRun(outcome);
            });
            // iterate: inconclusive
            foreach (var testCase in testRun.TestCases.Where(i => i.Inconclusive))
            {
                DoUpdateTestResult(testCase, inline: true);
            }

            // test plan
            AttachToTestPlan(testRun);

            // close
            testRun.Close(jiraClient, resolution: "Done");
        }

        private void AttachToTestPlan(RhinoTestRun testRun)
        {
            // attach to plan (if any)
            var plans = testRun.TestCases.SelectMany(i => (List<string>)i.Context["testPlans"]).Distinct();

            // exit conditions
            if (!plans.Any())
            {
                return;
            }

            // build commands
            var commands = plans.Select(i => RavenCommandsRepository.AssociateExecutions(i, new[] { testRun.Key }));

            // send
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };
            Parallel.ForEach(commands, options, command => ravenClient.SendCommand(command));
        }
        #endregion

        #region *** Put: Test Results ***
        /// <summary>
        /// Updates a single test results iteration under automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update results.</param>
        public override void UpdateTestResult(RhinoTestCase testCase)
        {
            DoUpdateTestResult(testCase, inline: false);
        }
        #endregion

        #region *** Bugs & Defects    ***
        /// <summary>
        /// Gets a list of open bugs.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to find bugs.</param>
        /// <returns>A list of bugs (can be JSON or ID for instance).</returns>
        public override IEnumerable<string> GetBugs(RhinoTestCase testCase)
        {
            return DoGetBugs(testCase).Select(i => $"{i}");
        }

        /// <summary>
        /// Asserts if the RhinoTestCase has already an open bug.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to assert against match bugs.</param>
        /// <returns>An open bug.</returns>
        public override string GetOpenBug(RhinoTestCase testCase)
        {
            // setup
            var bugs = DoGetBugs(testCase);

            // get
            var openBug = bugs.Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false));

            // assert
            return openBug.Any() ? $"{openBug.First()}" : string.Empty;
        }

        /// <summary>
        /// Creates a new bug under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider bug.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string OnCreateBug(RhinoTestCase testCase)
        {
            // exit conditions
            if (testCase.Actual)
            {
                return string.Empty;
            }

            // create bug
            return DoCreateBug(testCase);
        }

        /// <summary>
        /// Updates an existing bug (partial updates are supported, i.e. you can submit and update specific fields only).
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update automation provider bug.</param>
        public override string OnUpdateBug(RhinoTestCase testCase)
        {
            // get existing bugs
            var isBugs = testCase.Context.ContainsKey("bugs") && testCase.Context["bugs"] != default;
            var bugs = isBugs ? (IEnumerable<string>)testCase.Context["bugs"] : Array.Empty<string>();

            // exit conditions
            if (bugs.All(i => string.IsNullOrEmpty(i)))
            {
                return "-1";
            }

            // possible duplicates
            if (bugs.Count() > 1)
            {
                var issues = jiraClient.Get(idsOrKeys: bugs).Where(i => testCase.IsBugMatch(bug: i, assertDataSource: true));

                var onBugs = issues
                    .OrderBy(i => $"{i["key"]}")
                    .Skip(1)
                    .Select(i => $"{i.SelectToken("key")}")
                    .Where(i => !string.IsNullOrEmpty(i));

                DoCloseBugs(testCase, resolution: "Duplicate", bugs: onBugs);
            }

            // update
            bugs = jiraClient
                .Get(idsOrKeys: bugs)
                .Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false))
                .Select(i => $"{i.SelectToken("key")}")
                .Where(i => !string.IsNullOrEmpty(i));

            testCase.UpdateBug(idOrKey: bugs.FirstOrDefault(), jiraClient);

            // get
            return $"{Utilities.GetUrl(Configuration.ConnectorConfiguration.Collection)}/browse/{bugs.FirstOrDefault()}";
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override IEnumerable<string> OnCloseBugs(RhinoTestCase testCase)
        {
            // get existing bugs
            var isBugs = testCase.Context.ContainsKey("bugs") && testCase.Context["bugs"] != default;
            var bugs = isBugs ? (IEnumerable<string>)testCase.Context["bugs"] : Array.Empty<string>();

            // get conditions (double check for bugs)
            if (!bugs.Any())
            {
                bugs = DoGetBugs(testCase).Select(i => $"{i}");
            }

            // close bugs
            return DoCloseBugs(testCase, resolution: "Done", bugs);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override string OnCloseBug(RhinoTestCase testCase)
        {
            // get existing bugs
            var isBugs = testCase.Context.ContainsKey("bugs") && testCase.Context["bugs"] != default;
            var contextBugs = isBugs ? (IEnumerable<string>)testCase.Context["bugs"] : Array.Empty<string>();
            var bugs = jiraClient.Get(idsOrKeys: contextBugs).Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false));

            // get conditions (double check for bugs)
            if (!bugs.Any())
            {
                return string.Empty;
            }

            // close bugs: first
            var onBug = $"{bugs.FirstOrDefault()?.SelectToken("key")}";
            testCase.CloseBug(bugIssueKey: onBug, "Done", jiraClient);

            // close bugs: duplicate (if any)
            foreach (var bug in bugs.Skip(1))
            {
                testCase.CloseBug($"{bug.SelectToken("key")}", "Duplicate", jiraClient);
            }
            return onBug;
        }

        private IEnumerable<string> DoCloseBugs(RhinoTestCase testCase, string resolution, IEnumerable<string> bugs)
        {
            // close bugs
            var closedBugs = new List<string>();
            foreach (var bug in bugs)
            {
                var isClosed = testCase.CloseBug(bugIssueKey: bug, resolution: resolution, jiraClient);

                // logs
                if (isClosed)
                {
                    closedBugs.Add($"{Utilities.GetUrl(Configuration.ConnectorConfiguration.Collection)}/browse/{bug}");
                    continue;
                }
                logger?.Error($"Close-Bug -Bug [{bug}] -Test [{testCase.Key}] = false");
            }
            return closedBugs;
        }

        private IEnumerable<JToken> DoGetBugs(RhinoTestCase testCase)
        {
            // shortcuts
            var bugType = $"{capabilities[AtlassianCapabilities.BugType]}";
            const string typePath = "fields.issuetype.name";
            const string statusPath = "fields.status.name";

            // get test issue
            var test = jiraClient.Get(testCase.Key);

            // get bugs
            var bugsKeys = test
                .SelectTokens("..inwardIssue")
                .Where(i => $"{i.SelectToken(typePath)}"?.Equals(bugType) == true && $"{i.SelectToken(statusPath)}"?.Equals("Closed") != true)
                .Select(i => $"{i["key"]}")
                .ToArray();

            // add to context
            testCase.Context["bugs"] = bugsKeys;

            // get issues
            return jiraClient.Get(bugsKeys);
        }

        private string DoCreateBug(RhinoTestCase testCase)
        {
            // get bug response
            var response = testCase.CreateBug(jiraClient);

            // results
            return response == default
                ? "-1"
                : $"{Utilities.GetUrl(Configuration.ConnectorConfiguration.Collection)}/browse/{response["key"]}";
        }
        #endregion

        // UTILITIES
        private void DoUpdateTestResult(RhinoTestCase testCase, bool inline)
        {
            try
            {
                // setup
                var forUploadOutcomes = new[] { "PASS", "FAIL" };

                // exit conditions
                var outcome = "TODO";
                if (testCase.Context.ContainsKey("outcome"))
                {
                    outcome = $"{testCase.Context["outcome"]}";
                }

                // update
                if (inline)
                {
                    testCase.SetOutcomeBySteps();
                    testCase.SetOutcomeByRun();
                    return;
                }
                testCase.SetOutcomeBySteps();

                // attachments
                if (forUploadOutcomes.Contains(outcome.ToUpper()))
                {
                    testCase.UploadEvidences();
                }

                // fail message
                if (outcome.Equals("FAIL", Compare) || testCase.Steps.Any(i => i.Exception != default))
                {
                    var comment = testCase.GetFailComment();
                    testCase.UpdateResultComment(comment);
                }
            }
            catch (Exception e) when (e != null)
            {
                logger?.Error($"Update-TestResult -Test [{testCase.Key}] -Inline [{inline}] = false", e);
            }
        }
    }
}