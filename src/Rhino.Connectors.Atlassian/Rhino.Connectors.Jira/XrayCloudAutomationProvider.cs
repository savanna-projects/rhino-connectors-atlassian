// 0hshf1gBkfZqsoABp9oO173D
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Api;
using Rhino.Api.Contracts.Attributes;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Cloud.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Utilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.Xray.Cloud
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
        private readonly JiraCommandsExecutor executor;

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
            executor = new JiraCommandsExecutor(configuration.GetJiraAuthentication());

            // capabilities
            BucketSize = configuration.GetBuketSize();
            configuration.PutIssueTypes();
            capabilities = configuration.Capabilities.ContainsKey($"{Connector.JiraXryCloud}:options")
                ? configuration.Capabilities[$"{Connector.JiraXryCloud}:options"] as IDictionary<string, object>
                : new Dictionary<string, object>();
        }
        #endregion

        #region *** Get: Test Cases   ***
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
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };
            Parallel.ForEach(ids, options, id => map[id] = jiraClient.GetIssueType(idOrKey: id));

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
            return testCases;
        }

        // gets a collection of RhinoTestCase based on test issue
        private IEnumerable<RhinoTestCase> GetByTests(IEnumerable<string> idsOrKeys)
        {
            // setup
            var testCases = xpandClient.GetTestCases(idsOrKeys);

            // convert
            return ProcessTests(testCases);
        }

        // gets a collection of RhinoTestCase based on test set issue
        private IEnumerable<RhinoTestCase> GetBySets(params string[] issueKeys)
        {
            // setup
            var testCases = xpandClient.GetTestsBySets(issueKeys);

            // convert
            return ProcessTests(testCases);
        }

        // gets a collection of RhinoTestCase based on test plan issue
        private IEnumerable<RhinoTestCase> GetByPlans(params string[] issueKeys)
        {
            // setup
            var testCases = xpandClient.GetTestsByPlans(issueKeys);

            // convert
            return ProcessTests(testCases);
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
            // constants: logging
            const string M = "Create-Test -Project [{0}] -Set [{1}] = true";

            // create jira issue
            var issue = CreateTestOnJira(testCase);

            // apply to context
            testCase.Context["jira-issue-id"] = issue == default ? string.Empty : $"{issue["id"]}";

            // create test steps
            CreateTestSteps(testCase);

            // create & apply preconditions
            var testCaseKey = $"{issue["key"]}";
            var precondition = CreatePrecondition(testCaseKey, testCase.DataSource);
            xpandClient.AddPrecondition($"{precondition.SelectToken("id")}", testCaseKey);

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.CreateComment(idOrKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, Configuration.ConnectorConfiguration.Project, string.Join(", ", testCase?.TestSuites));

            // results
            return $"{issue}";
        }

        private JToken CreateTestOnJira(RhinoTestCase testCase)
        {
            // shortcuts
            var onProject = Configuration.ConnectorConfiguration.Project;
            var testType = $"{Configuration.Capabilities[AtlassianCapabilities.TestType]}";

            // setup context
            testCase.Context["issuetype-id"] = $"{jiraClient.GetIssueTypeFields(idOrKey: testType, path: "id")}";
            testCase.Context["project-key"] = onProject;

            // setup request body
            var issue = jiraClient.Create(testCase.ToJiraCreateExecutionRequest()).AsJObject();
            if (issue == default || !issue.ContainsKey("id"))
            {
                logger?.Fatal("Was not able to create a test case.");
                return default;
            }
            return issue;
        }

        private void CreateTestSteps(RhinoTestCase testCase)
        {
            // setup
            var commands = testCase.ToXrayStepsCommands();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

            // create
            Parallel.ForEach(commands, options, command => command.Send(executor));
        }

        private JToken CreatePrecondition(string issueKey, IEnumerable<IDictionary<string, object>> dataSource)
        {
            // constants: logging
            const string M = "Preconditions [{0}] created under project [{1}].";

            // shortcuts
            var onProject = Configuration.ConnectorConfiguration.Project;
            var preconditionsType = $"{Configuration.Capabilities[AtlassianCapabilities.PreconditionsType]}";
            var id = $"{jiraClient.GetIssueTypeFields(idOrKey: preconditionsType, path: "id")}";

            // get precondition markdown
            var markdown = dataSource.ToMarkdown().Replace("\\r\\n", "\r\n");

            // setup request data
            var data = new
            {
                Field = new Dictionary<string, object>
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
                }
            };

            // send
            var issue = jiraClient.Create(data).AsJObject();

            // exit conditions
            if (issue == default || !issue.ContainsKey("id"))
            {
                logger?.Fatal($"Was not able to create preconditions for [{issueKey}]");
                return default;
            }

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.CreateComment(idOrKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, $"{issue["key"]}", Configuration.ConnectorConfiguration.Project);
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
            xpandClient.AddTestsToExecution(testRun.Key, testRun.TestCases.Select(i => i.Key).ToArray());

            // get execution details for all tests (run on distinct tests for payload efficiency)
            PutExecutionDetails(testRun);

            // updated test run
            return testRun;
        }

        private void CreateOnJira(RhinoTestRun testRun)
        {
            // create execution issue
            var requestBody = Assembly.GetExecutingAssembly().ReadEmbeddedResource("create_test_execution_xray.txt")
                .Replace("[project-key]", Configuration.ConnectorConfiguration.Project)
                .Replace("[run-title]", TestRun.Title)
                .Replace("[type-name]", $"{capabilities[AtlassianCapabilities.ExecutionType]}")
                .Replace("[assignee]", Configuration.ConnectorConfiguration.User);

            // setup
            var responseBody = JiraCommandsRepository.Create(requestBody).Send(executor).AsJToken();

            // setup
            testRun.Key = $"{responseBody["key"]}";
            testRun.Link = $"{responseBody["self"]}";
            testRun.Context["runtimeid"] = $"{responseBody["id"]}";
            testRun.Context["testRun"] = responseBody;
        }

        private void PutExecutionDetails(RhinoTestRun testRun)
        {
            // setup
            var detailsMap = new ConcurrentBag<(string Key, JToken Details)>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

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
                    onTest.Context["testRun"] = testRun.Context["testRun"];
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
        private IEnumerable<RhinoTestCase> ProcessTests(IEnumerable<JToken> testCases)
        {
            // exit conditions
            if (!testCases.Any())
            {
                return Array.Empty<RhinoTestCase>();
            }

            // parse into connector test case
            var onTestCases = testCases.Select(i => i.ToRhinoTestCase());

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
                onTestCases = onMethods.First().Invoke(this, new[] { onTestCases }) as IEnumerable<RhinoTestCase>;
            }

            // complete
            return onTestCases;
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

        // GET TEST CASES PIPELINE METHODS
        // puts the first test set founds under each test case as RhinoTestCase.TestSuite
        [Pipeline(order: 0)]
        private IEnumerable<RhinoTestCase> SetTestSuites(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

            // iterate
            Parallel.ForEach(testCases, options, testCase => onTestCases.Add(SetTestSuites(testCase)));

            // results
            return onTestCases;
        }

        private RhinoTestCase SetTestSuites(RhinoTestCase testCase)
        {
            // setup conditions
            var isContext = testCase.Context.ContainsKey(nameof(testCase));
            var isToken = isContext && testCase.Context[nameof(testCase)] is JToken;

            // exit conditions
            if (!isToken)
            {
                logger?.Error($"Get-ContextEntry -Test [{testCase.Key}] -Entry [testCase] = false");
                return testCase;
            }

            // get
            var onTestCase = (JToken)testCase.Context[nameof(testCase)];

            // put
            testCase.TestSuites = xpandClient.GetSetsByTest($"{onTestCase["id"]}", $"{onTestCase["key"]}");
            return testCase;
        }

        // puts all test plans related to these tests in the test context
        [Pipeline(order: 1)]
        private IEnumerable<RhinoTestCase> SetTestPlans(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

            // iterate
            Parallel.ForEach(testCases, options, onTestCase => onTestCases.Add(SetTestPlans(onTestCase)));

            // results
            return onTestCases;
        }

        private RhinoTestCase SetTestPlans(RhinoTestCase testCase)
        {
            // setup conditions
            var isContext = testCase.Context.ContainsKey(nameof(testCase));
            var isToken = isContext && testCase.Context[nameof(testCase)] is JToken;

            // exit conditions
            if (!isToken)
            {
                logger?.Error($"Get-ContextEntry -Test [{testCase.Key}] -Entry [testCase] = false");
                return testCase;
            }

            // get
            var onTestCase = (JToken)testCase.Context[nameof(testCase)];

            // put
            testCase.Context["testPlans"] = xpandClient.GetPlansByTest($"{onTestCase["id"]}", $"{onTestCase["key"]}");
            return testCase;
        }

        // puts a combined data source from each test preconditions into RhinoTestCase.DataSource
        [Pipeline(order: 2)]
        private IEnumerable<RhinoTestCase> SetDataSource(IEnumerable<RhinoTestCase> testCases)
        {
            // setup
            var onTestCases = new ConcurrentBag<RhinoTestCase>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

            // iterate
            Parallel.ForEach(testCases, options, onTestCase => onTestCases.Add(SetDataSource(onTestCase)));

            // results
            return onTestCases;
        }

        private RhinoTestCase SetDataSource(RhinoTestCase testCase)
        {
            // setup conditions
            var isContext = testCase.Context.ContainsKey(nameof(testCase));
            var isToken = isContext && testCase.Context[nameof(testCase)] is JToken;

            // exit conditions
            if (!isToken)
            {
                logger?.Error($"Get-ContextEntry -Test [{testCase.Key}] -Entry [testCase] = false");
                return testCase;
            }

            // get
            var onTestCase = (JToken)testCase.Context[nameof(testCase)];
            var preconditions = xpandClient.GetPreconditionsByTest($"{onTestCase["id"]}", $"{onTestCase["key"]}");

            // exit conditions
            if (!preconditions.Any())
            {
                return testCase;
            }

            // merge
            var dataSource = jiraClient
                .Get(preconditions)
                .Select(i => new DataTable().FromMarkDown($"{i.SelectToken("fields.description")}".Trim(), default))
                .Merge();

            // put
            testCase.DataSource = dataSource.ToDictionary().Cast<Dictionary<string, object>>().ToArray();
            return testCase;
        }
        #endregion

        // UTILITIES
        private void DoUpdateTestResults(RhinoTestCase testCase)
        {
            // constants
            const string ContextKey = "executionDetails";

            // setup
            var executionDetails = testCase.Context.ContainsKey(ContextKey) && testCase.Context[ContextKey] != default
                ? (JToken)testCase.Context[ContextKey]
                : JToken.Parse("{}");
            var run = $"{executionDetails.SelectToken("_id")}";
            var execution = $"{executionDetails.SelectToken("testExecIssueId")}";
            var steps = executionDetails.SelectToken("steps").Select(i => $"{i["id"]}").Where(i => i != default).ToArray();
            var project = $"{jiraClient.ProjectMeta.SelectToken("id")}";

            try
            {
                // exit conditions
                if (!testCase.Context.ContainsKey("outcome") || $"{testCase.Context["outcome"]}".Equals("EXECUTING", Compare))
                {
                    xpandClient.UpdateTestRunStatus((execution, testCase.TestRunKey), project, run, "EXECUTING");
                    logger.Trace($"Get-TestStatus [{testCase.Key}] = EXECUTING");
                    return;
                }

                // build
                var testSteps = new List<(string, string)>();
                for (int i = 0; i < steps.Length; i++)
                {
                    var result = testCase.Steps.ElementAt(i).Actual ? "PASSED" : "FAILED";
                    testSteps.Add((steps[i], result));
                }

                // apply on steps
                var options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };
                Parallel.ForEach(testSteps, options, testStep
                    => xpandClient.UpdateStepStatus((execution, testCase.TestRunKey), run, testStep));

                // apply on test
                xpandClient.UpdateTestRunStatus((execution, testCase.TestRunKey), project, run, $"{testCase.Context["outcome"]}");
            }
            catch (Exception e) when (e != null)
            {
                logger?.Error($"Update-TestResults -Execution [{execution}] = false", e);
            }
        }
    }
}