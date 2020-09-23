// 0hshf1gBkfZqsoABp9oO173D
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api;
using Rhino.Api.Contracts.Attributes;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.Xray.Cloud.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Utilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray.Cloud.Framework
{
    public class XrayCloudAutomationProvider : ProviderManager
    {
        // members: constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        // state: global parameters
        private readonly ILogger logger;
        private readonly JiraClient jiraClient;
        private readonly XpandClient xpandClient;
        private readonly IDictionary<string, object> capabilities;
        private readonly int bucketSize;

        #region *** Constructors      ***
        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration)
            : this(configuration, Utilities.Types)
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types)
            : this(configuration, types, Utilities.CreateDefaultLogger(configuration))
        { }

        /// <summary>
        /// Creates a new instance of this Rhino.Api.Simulator.Framework.XrayAutomationProvider.
        /// </summary>
        /// <param name="configuration">Rhino.Api.Contracts.Configuration.RhinoConfiguration to use with this provider.</param>
        /// <param name="types">A collection of <see cref="Type"/> to load for this repository.</param>
        /// <param name="logger">Gravity.Abstraction.Logging.ILogger implementation for this provider.</param>
        public XrayCloudAutomationProvider(RhinoConfiguration configuration, IEnumerable<Type> types, ILogger logger)
            : base(configuration, types, logger)
        {
            // setup
            this.logger = logger?.Setup(loggerName: nameof(XrayCloudAutomationProvider));
            jiraClient = new JiraClient(configuration.GetJiraAuthentication());
            xpandClient = new XpandClient(configuration.GetJiraAuthentication());

            // capabilities
            bucketSize = configuration.GetBuketSize();
            configuration.PutIssueTypes();
            capabilities = configuration.ProviderConfiguration.Capabilities;
        }
        #endregion

        #region *** GET: Test Cases   ***
        /// <summary>
        /// Returns a list of test cases for a project.
        /// </summary>
        /// <param name="ids">A list of test ids to get test cases by.</param>
        /// <returns>A collection of Rhino.Api.Contracts.AutomationProvider.RhinoTestCase</returns>
        public override IEnumerable<RhinoTestCase> OnGetTestCases(params string[] ids)
        {
            // setup: issues map
            var map = new ConcurrentDictionary<string, string>();

            // build issues map
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(ids, options, id => map[id] = jiraClient.GetIssueType(issueKey: id));

            // entities
            var byTests = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.TestType]}", Compare)).Select(i => i.Key);
            var bySets = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.SetType]}", Compare)).Select(i => i.Key);
            var byPlans = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.PlanType]}", Compare)).Select(i => i.Key);

            // setup
            var testCases = new ConcurrentBag<RhinoTestCase>();

            // get and apply
            var onTestCases = GetByTests(byTests.ToArray());
            testCases.AddRange(onTestCases);

            onTestCases = GetBySets(bySets.ToArray());
            testCases.AddRange(onTestCases);

            onTestCases = GetByPlans(byPlans.ToArray());
            testCases.AddRange(onTestCases);

            // results
            // TODO: remove .DistinctBy(i => i.Key) on the next RhinoApi Update
            return testCases.DistinctBy(i => i.Key);
        }

        // gets a collection of RhinoTestCase based on test issue
        private IEnumerable<RhinoTestCase> GetByTests(params string[] issueKeys)
        {
            return DoGetByTests(issueKeys);
        }

        // gets a collection of RhinoTestCase based on test set issue
        private IEnumerable<RhinoTestCase> GetBySets(params string[] issueKeys)
        {
            // setup
            var onkeys = xpandClient.GetTestsBySets(bucketSize, issueKeys).Select(i => $"{i["key"]}");

            // get tests
            return DoGetByTests(onkeys.ToArray());
        }

        // gets a collection of RhinoTestCase based on test plan issue
        private IEnumerable<RhinoTestCase> GetByPlans(params string[] issueKeys)
        {
            // setup
            var onkeys = xpandClient.GetTestsByPlans(bucketSize, issueKeys).Select(i => $"{i["key"]}");

            // get tests
            return DoGetByTests(onkeys.ToArray());
        }
        #endregion

        #region *** CREATE: Test Case ***
        /// <summary>
        /// Creates a new test case under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider test case.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string CreateTestCase(RhinoTestCase testCase)
        {
            // constants: logging
            const string M = "Test created under project [{0}] and assigned to [{1}] test set.";

            // create jira issue
            var issue = CreateJiraTestCaseIssue(testCase);

            // apply to context
            testCase.Context["jira-issue-id"] = issue == default ? string.Empty : $"{issue["id"]}";

            // create test steps
            PutTestSteps(bucketSize, testCase);

            // create & apply preconditions
            var testCaseKey = $"{issue["key"]}";
            var precondition = CreatePrecondition(testCaseKey, testCase.DataSource);
            xpandClient.AddPrecondition($"{precondition.SelectToken("id")}", testCaseKey);

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.AddComment(issueKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, Configuration.ProviderConfiguration.Project, testCase?.TestSuite);

            // results
            return $"{issue}";
        }

        private JObject CreateJiraTestCaseIssue(RhinoTestCase testCase)
        {
            // shortcuts
            var onProject = Configuration.ProviderConfiguration.Project;
            var testType = $"{Configuration.ProviderConfiguration.Capabilities[AtlassianCapabilities.TestType]}";

            // setup context
            testCase.Context["issuetype-id"] = $"{jiraClient.GetIssueTypeFields(issueType: testType, path: "id")}";
            testCase.Context["project-key"] = onProject;

            // setup request body
            var requestBody = testCase.ToJiraCreateExecutionRequest();
            var issue = jiraClient.CreateIssue(requestBody);
            if (issue == default || !issue.ContainsKey("id"))
            {
                logger?.Fatal("Was not able to create a test case.");
                return default;
            }
            return issue;
        }

        private void PutTestSteps(int bucketSize, RhinoTestCase testCase)
        {
            // setup test steps
            var stepRequests = testCase.ToXrayStepsRequests();
            var client = xpandClient.GetClientWithToken(testCase.Key);
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };

            Parallel.ForEach(stepRequests, options, request =>
            {
                var response = client.PostAsync(request.Endpoint, request.Content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    logger?.Fatal($"Was unable to create test step for test [{testCase.Key}].");
                }
            });
        }

        private JObject CreatePrecondition(string issueKey, IEnumerable<IDictionary<string, object>> dataSource)
        {
            // constants: logging
            const string M = "Preconditions [{0}] created under project [{1}].";

            // shortcuts
            var onProject = Configuration.ProviderConfiguration.Project;
            var preconditionsType = $"{Configuration.ProviderConfiguration.Capabilities[AtlassianCapabilities.PreconditionsType]}";
            var id = $"{jiraClient.GetIssueTypeFields(issueType: preconditionsType, path: "id")}";

            // get precondition markdown
            var markdown = dataSource.ToMarkdown().Replace("\\r\\n", "\r\n");

            // setup request
            var requestObjt = new Dictionary<string, object>
            {
                ["summary"] = $"Data Set: Created for [{issueKey}]",
                ["description"] = markdown,
                ["issuetype"] = new Dictionary<string, object>
                {
                    ["id"] = id
                },
                ["project"] = new Dictionary<string, object>
                {
                    ["key"] = onProject
                }
            };
            var requestBody = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["fields"] = requestObjt
            });

            // send
            var issue = jiraClient.CreateIssue(requestBody);

            // exit conditions
            if (issue == default || !issue.ContainsKey("id"))
            {
                logger?.Fatal($"Was not able to create preconditions for [{issueKey}]");
                return default;
            }

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.AddComment(issueKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, $"{issue["key"]}", Configuration.ProviderConfiguration.Project);
            return issue;
        }
        #endregion

        #region *** CREATE: Test Run  ***
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

            // create jira issue
            CreateOnJira(testRun);

            // apply tests
            xpandClient.AddTestsToExecution(bucketSize, testRun.Key, testRun.TestCases.Select(i => i.Key).ToArray());

            // get execution details for all tests (run on distinct tests for payload efficiency)
            PutExecutionDetails(testRun);

            // updated test run
            return testRun;
        }

        private void CreateOnJira(RhinoTestRun testRun)
        {
            // create execution issue
            var requestBody = Assembly.GetExecutingAssembly().ReadEmbeddedResource("create_test_execution_xray.txt")
                .Replace("[project-key]", Configuration.ProviderConfiguration.Project)
                .Replace("[run-title]", TestRun.Title)
                .Replace("[type-name]", $"{Configuration.ProviderConfiguration.Capabilities[AtlassianCapabilities.ExecutionType]}")
                .Replace("[assignee]", Configuration.ProviderConfiguration.User);
            var responseBody = jiraClient.CreateIssue(requestBody);

            // setup
            testRun.Key = $"{responseBody["key"]}";
            testRun.Link = $"{responseBody["self"]}";
            testRun.Context["runtimeid"] = $"{responseBody["id"]}";
        }

        private void PutExecutionDetails(RhinoTestRun testRun)
        {
            // setup
            var detailsMap = new ConcurrentBag<(string Key, JObject Details)>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };

            // get
            Parallel.ForEach(testRun.TestCases.DistinctBy(i => i.Key), options, testCase =>
            {
                var details = xpandClient.GetExecutionDetails(testRun.Key, testCase.Key);
                detailsMap.Add((testCase.Key, details));
            });

            // apply
            foreach (var (Key, Details) in detailsMap)
            {
                foreach (var onTest in testRun.TestCases.Where(i => i.Key.Equals(Key, Compare)))
                {
                    onTest.Context["executionDetails"] = Details;
                }
            }
        }

        //// TODO: implement persistent retry (until all done or until timeout)        
        ///// <summary>
        ///// Completes automation provider test run results, if any were missed or bypassed.
        ///// </summary>
        ///// <param name="testRun">Rhino.Api.Contracts.AutomationProvider.RhinoTestRun results object to complete by.</param>
        //public override void CompleteTestRun(RhinoTestRun testRun)
        //{
        //    // exit conditions
        //    if (Configuration.IsDryRun())
        //    {
        //        return;
        //    }

        //    // setup: failed to update
        //    var inStatus = new[] { "TODO", "EXECUTING" };

        //    // get all test keys to re-assign outcome
        //    var testResults = testRun
        //        .GetTests()
        //        .Where(i => inStatus.Contains($"{i["status"]}"))
        //        .Select(i => $"{i["key"]}");

        //    // iterate: pass/fail
        //    foreach (var testCase in testRun.TestCases.Where(i => testResults.Contains(i.Key) && !i.Inconclusive))
        //    {
        //        DoUpdateTestResults(testCase);
        //    }
        //    // iterate: inconclusive
        //    foreach (var testCase in testRun.TestCases.Where(i => i.Inconclusive))
        //    {
        //        DoUpdateTestResults(testCase);
        //    }

        //    // test plan
        //    AttachToTestPlan(testRun);

        //    // close
        //    testRun.Close(jiraClient, resolution: "Done");
        //}

        //// TODO: implement raven v2.0 for assign test execution to test plan when available
        //private void AttachToTestPlan(RhinoTestRun testRun)
        //{
        //    // attach to plan (if any)
        //    var plans = testRun.TestCases.SelectMany(i => (List<string>)i.Context["testPlans"]).Distinct();

        //    // exit conditions
        //    if (!plans.Any())
        //    {
        //        return;
        //    }

        //    // build request
        //    var requests = new List<(string Endpoint, StringContent Content)>();
        //    const string endpointFormat = "/rest/raven/1.0/testplan/{0}/testexec";
        //    foreach (var plan in plans)
        //    {
        //        var palyload = new
        //        {
        //            Assignee = Configuration.ProviderConfiguration.User,
        //            Keys = new[] { testRun.Key }
        //        };
        //        var requestBody = JsonConvert.SerializeObject(palyload, JiraClient.JsonSettings);
        //        var enpoint = string.Format(endpointFormat, plan);
        //        var content = new StringContent(requestBody, Encoding.UTF8, JiraClient.MediaType);
        //        requests.Add((enpoint, content));
        //    }

        //    // send
        //    var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
        //    Parallel.ForEach(requests, options, request
        //        => JiraClient.HttpClient.PostAsync(request.Endpoint, request.Content).GetAwaiter().GetResult());
        //}
        #endregion

        #region *** PUT: Test Run     ***
        /// <summary>
        /// Updates a single test results iteration under automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update results.</param>
        public override void UpdateTestResult(RhinoTestCase testCase)
        {
            // validate (double check on execution details)
            if (!testCase.Context.ContainsKey("executionDetails"))
            {
                testCase.Context["executionDetails"] = xpandClient.GetExecutionDetails("", testCase.Key);
            }

            // update
            DoUpdateTestResults(testCase);
        }
        #endregion

        #region *** Process Test      ***
        private IEnumerable<RhinoTestCase> DoGetByTests(params string[] issueKeys)
        {
            // exit conditions
            if (issueKeys.Length == 0)
            {
                return Array.Empty<RhinoTestCase>();
            }

            // parse into connector test case
            var testCases = Get(issueKeys);

            // get all pipeline methods
            var methods = new List<MethodInfo>(GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(i => i.GetCustomAttribute<PipelineAttribute>() != null));

            // order execute
            for (int i = 0; i < methods.Count; i++)
            {
                var onMethods = methods.Where(m => m.GetCustomAttribute<PipelineAttribute>().Order == i);
                if (!onMethods.Any())
                {
                    continue;
                }
                OnMethodsError(methods: onMethods);
                testCases = onMethods.First().Invoke(this, new[] { testCases }) as IEnumerable<RhinoTestCase>;
            }

            // complete
            return testCases;
        }

        private void OnMethodsError(IEnumerable<MethodInfo> methods)
        {
            if (methods.Count() > 1)
            {
                var consolidate = string.Join(", ", methods.Select(i => i.Name));
                var message =
                    $"Pipeline methods [{consolidate}] have the same order index." +
                    " Please make sure each method has a unique PiplineAttribute.Order value.";
                throw new InvalidOperationException(message);
            }
        }

        // gets a collection of RhinoTestCase by issue keys
        private IEnumerable<RhinoTestCase> Get(params string[] issueKeys)
        {
            // parse into JObject
            var jsonObjects = xpandClient.GetTestCases(bucketSize, issueKeys);

            // parse into connector test case
            var testCases = !jsonObjects.Any() ? Array.Empty<RhinoTestCase>() : jsonObjects.Select(i => i.ToRhinoTestCase());
            if (!testCases.Any())
            {
                return Array.Empty<RhinoTestCase>();
            }
            return testCases;
        }

        // GET TEST CASES PIPELINE METHODS
        // puts the first test set founds under each test case as RhinoTestCase.TestSuite
        [Pipeline(order: 0)]
        private IEnumerable<RhinoTestCase> PutTestSuite(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();

            // iterate
            foreach (var testCase in testCases)
            {
                var isContext = testCase.Context.ContainsKey(nameof(testCase)) && testCase.Context[nameof(testCase)] is JObject;
                if (!isContext)
                {
                    logger?.Error($"Test case [{testCase.Key}] does not have [testCase] context entry");
                    continue;
                }
                var jsonObject = (JObject)testCase.Context[nameof(testCase)];

                var testSets = xpandClient.GetSetsByTest(jsonObject);
                if (!testSets.Any())
                {
                    continue;
                }
                testCase.TestSuite = $"{jiraClient.GetIssue(issueKey: testSets.First())["key"]}";
                onTestCases.Add(testCase);
            }

            // results
            return onTestCases;
        }

        // puts all test plans related to these tests in the test context
        [Pipeline(order: 1)]
        private IEnumerable<RhinoTestCase> PutTestPlans(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();

            // iterate
            foreach (var testCase in testCases)
            {
                var isContext = testCase.Context.ContainsKey(nameof(testCase)) && testCase.Context[nameof(testCase)] is JObject;
                if (!isContext)
                {
                    onTestCases.Add(testCase);
                    logger?.Error($"Test case [{testCase.Key}] does not have [testCase] context entry");
                    continue;
                }
                var jsonObject = (JObject)testCase.Context[nameof(testCase)];

                var testPlans = xpandClient.GetPlansByTest(jsonObject);
                if (!testPlans.Any())
                {
                    onTestCases.Add(testCase);
                    testCase.Context["testPlans"] = Array.Empty<string>();
                    continue;
                }
                testCase.Context["testPlans"] = jiraClient.GetIssues(bucketSize, testPlans.ToArray()).Select(i => $"{i["key"]}");
                onTestCases.Add(testCase);
            }

            // results
            return onTestCases;
        }

        // puts a combined data source from each test preconditions into RhinoTestCase.DataSource
        [Pipeline(order: 2)]
        private IEnumerable<RhinoTestCase> PutDataSource(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();

            // iterate
            foreach (var testCase in testCases)
            {
                var isContext = testCase.Context.ContainsKey(nameof(testCase)) && testCase.Context[nameof(testCase)] is JObject;
                if (!isContext)
                {
                    onTestCases.Add(testCase);
                    logger?.Error($"Test case [{testCase.Key}] does not have [testCase] context entry");
                    continue;
                }
                var jsonObject = (JObject)testCase.Context[nameof(testCase)];

                var preconditions = xpandClient.GetPreconditionsByTest(jsonObject);
                if (!preconditions.Any())
                {
                    onTestCases.Add(testCase);
                    continue;
                }

                var mergedDataSource = preconditions
                    .Select(i => new DataTable().FromMarkDown($"{jiraClient.GetIssue($"{i}").SelectToken("fields.description")}".Trim(), default))
                    .Merge();
                testCase.DataSource = mergedDataSource.ToDictionary().Cast<Dictionary<string, object>>().ToArray();

                onTestCases.Add(testCase);
            }

            // results
            return onTestCases;
        }
        #endregion

        // UTILITIES
        private void DoUpdateTestResults(RhinoTestCase testCase)
        {
            try
            {
                // setup
                var executionDetails = (JObject)testCase.Context["executionDetails"];
                var run = $"{executionDetails["_id"]}";
                var steps = executionDetails["steps"].Select(i => $"{i["id"]}").ToArray();

                // exit conditions
                if (!testCase.Context.ContainsKey("outcome") || $"{testCase.Context["outcome"]}".Equals("EXECUTING", Compare))
                {
                    xpandClient.PutTestRunStatus(testCase.TestRunKey, run, "EXECUTING");
                    logger.Trace($"Test {testCase.Key} is running. No status changes made.");
                    return;
                }

                // build
                var data = new List<(string, string)>();
                for (int i = 0; i < steps.Length; i++)
                {
                    var result = testCase.Steps.ElementAt(i).Actual ? "PASSED" : "FAILED";
                    data.Add((steps[i], result));
                }

                // apply on steps
                xpandClient.PutStepsRunStatus(testCase.TestRunKey, run, data.ToArray());

                // apply on test
                xpandClient.PutTestRunStatus(testCase.TestRunKey, run, $"{testCase.Context["outcome"]}");
            }
            catch (Exception e) when (e != null)
            {
                logger?.Error($"Failed to update test results for [{testCase.Key}]", e);
            }
        }
    }
}