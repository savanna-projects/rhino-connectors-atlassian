/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
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
        private readonly ParallelOptions options;
        private readonly JiraBugsManager bugsManager;

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
            configuration.PutDefaultCapabilities();
            capabilities = configuration.Capabilities.ContainsKey($"{Connector.JiraXryCloud}:options")
                ? configuration.Capabilities[$"{Connector.JiraXryCloud}:options"] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // misc
            options = new ParallelOptions { MaxDegreeOfParallelism = BucketSize };

            // integration
            bugsManager = new JiraBugsManager(jiraClient);
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
            Parallel.ForEach(ids, options, id => map[id] = jiraClient.GetIssueType(idOrKey: id));

            // entities
            var byTests = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.TestType]}", Compare)).Select(i => i.Key);
            var bySets = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.SetType]}", Compare)).Select(i => i.Key);
            var byPlans = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.PlanType]}", Compare)).Select(i => i.Key);
            var byExecutions = map.Where(i => i.Value.Equals($"{capabilities[AtlassianCapabilities.ExecutionType]}", Compare)).Select(i => i.Key);

            // setup
            var testCases = new ConcurrentBag<RhinoTestCase>();

            // get and apply
            var onTestCases = GetByTests(byTests);
            testCases.AddRange(onTestCases);

            onTestCases = GetBySets(bySets);
            testCases.AddRange(onTestCases);

            onTestCases = GetByPlans(byPlans);
            testCases.AddRange(onTestCases);

            onTestCases = GetByExecutions(byExecutions);
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
        private IEnumerable<RhinoTestCase> GetBySets(IEnumerable<string> idsOrKeys)
        {
            // setup
            var testCases = xpandClient.GetTestsBySets(idsOrKeys);

            // convert
            return ProcessTests(testCases);
        }

        // gets a collection of RhinoTestCase based on test plan issue
        private IEnumerable<RhinoTestCase> GetByPlans(IEnumerable<string> idsOrKeys)
        {
            // setup
            var testCases = xpandClient.GetTestsByPlans(idsOrKeys);

            // convert
            return ProcessTests(testCases);
        }

        // gets a collection of RhinoTestCase based on test execution issue
        private IEnumerable<RhinoTestCase> GetByExecutions(IEnumerable<string> idsOrKeys)
        {
            // setup
            var testCases = xpandClient.GetTestsByExecution(idsOrKeys);

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
            testCase.Key = $"{issue["key"]}";
            testCase.Context["jira-issue-id"] = issue == default ? string.Empty : $"{issue["id"]}";

            // create test steps
            CreateTestSteps(testCase);

            // create & apply preconditions            
            var precondition = CreatePrecondition(testCase.Key, testCase.DataSource);
            if (precondition != null)
            {
                xpandClient.AddPrecondition($"{precondition.SelectToken("id")}", testCase.Key);
            }

            // add to test sets
            var testSets = jiraClient
                .Get(idsOrKeys: testCase.TestSuites)
                .Select(i => (id: $"{i.SelectToken("id")}", key: $"{i.SelectToken("key")}"));

            Parallel.ForEach(testSets, options, testSet
                => xpandClient.AddTestsToSet(idAndKey: testSet, new[] { $"{issue.SelectToken("id")}" }));

            // comment
            var comment = Utilities.GetActionSignature(action: "created");
            jiraClient.AddComment(idOrKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, Configuration.ConnectorConfiguration.Project, string.Join(", ", testCase?.TestSuites));

            // results
            return $"{issue}";
        }

        private JToken CreateTestOnJira(RhinoTestCase testCase)
        {
            // shortcuts
            var onProject = Configuration.ConnectorConfiguration.Project;
            var testType = $"{capabilities[AtlassianCapabilities.TestType]}";

            // setup context
            testCase.Context["issuetype-id"] = $"{jiraClient.GetIssueTypeFields(idOrKey: testType, path: "id")}";
            testCase.Context["project-key"] = onProject;

            // setup request body
            var issue = jiraClient.Create(testCase.ToJiraCreateRequest()).AsJObject();
            if (issue == default || !issue.ContainsKey("id"))
            {
                logger?.Fatal("Was not able to create a test case.");
                return default;
            }

            // assign
            jiraClient.Assign(key: $"{issue.SelectToken("key")}");

            // get
            return issue;
        }

        // TODO: check how to implement parallel. current parallel behavior, affecting steps order.
        private void CreateTestSteps(RhinoTestCase testCase)
        {
            foreach (var command in testCase.ToXrayStepsCommands())
            {
                command.Send(executor);
            }
        }

        private JToken CreatePrecondition(string issueKey, IEnumerable<IDictionary<string, object>> dataSource)
        {
            // constants: logging
            const string M = "Preconditions [{0}] created under project [{1}].";

            // shortcuts
            var onProject = Configuration.ConnectorConfiguration.Project;
            var preconditionsType = $"{capabilities[AtlassianCapabilities.PreconditionsType]}";
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
            jiraClient.AddComment(idOrKey: issue["key"].ToString(), comment);

            // success
            Logger?.InfoFormat(M, $"{issue["key"]}", Configuration.ConnectorConfiguration.Project);
            return issue;
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

            // create jira issue
            CreateRunOnJira(testRun);

            // apply tests
            xpandClient.AddTestsToExecution(testRun.Key, testRun.TestCases.Select(i => i.Key).ToArray());

            // get execution details for all tests (run on distinct tests for payload efficiency)
            PutExecutionDetails(testRun);

            // updated test run
            return testRun;
        }

        private void CreateRunOnJira(RhinoTestRun testRun)
        {
            // setup
            var fields = new Dictionary<string, object>
            {
                ["project"] = new { Key = Configuration.ConnectorConfiguration.Project },
                ["summary"] = TestRun.Title,
                ["description"] = Utilities.GetActionSignature("created"),
                ["issuetype"] = new { Name = $"{capabilities[AtlassianCapabilities.ExecutionType]}" }
            };
            var data = new Dictionary<string, object>
            {
                ["fields"] = fields
            };

            // setup
            var response = jiraClient.Create(data);

            // put
            testRun.Key = $"{response.SelectToken("key")}";
            testRun.Link = $"{response.SelectToken("self")}";
            testRun.Context["runtimeid"] = $"{response.SelectToken("id")}";
            testRun.Context["testRun"] = response;

            // assign
            jiraClient.Assign(testRun.Key);
        }

        private void PutExecutionDetails(RhinoTestRun testRun)
        {
            // setup
            var detailsMap = new ConcurrentBag<(string Key, JToken Details)>();

            // get
            Parallel.ForEach(testRun.TestCases.DistinctBy(i => i.Key), options, testCase =>
            {
                var details = xpandClient.GetExecutionDetails(testRun.Key, testCase.Key);
                detailsMap.Add((testCase.Key, details));
            });

            // put execution details
            foreach (var (Key, Details) in detailsMap)
            {
                foreach (var onTest in testRun.TestCases.Where(i => i.Key.Equals(Key, Compare)))
                {
                    Put(testRun, onTest, Details);
                }
            }
        }

        private void Put(RhinoTestRun testRun, RhinoTestCase testCase, JToken details)
        {
            testCase.Context["executionDetails"] = details;
            testCase.Context["testRun"] = testRun.Context["testRun"];

            var onSteps = details.AsJObject().SelectToken("steps").Select(i => i.AsJObject());
            for (int i = 0; i < testCase.Steps.Count(); i++)
            {
                if (i > onSteps.Count() - 1)
                {
                    continue;
                }
                testCase.Steps.ElementAt(i).Context["runtimeid"] = $"{onSteps.ElementAt(i).SelectToken("id")}";
            }
        }
        #endregion

        #region *** Update: Test Run  ***     
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

            // get all test keys to re-assign outcome
            var testCases = testRun
                .TestCases
                .Where(i => !i.Context.ContainsKey("runUpdated") || !(bool)i.Context["runUpdated"]);

            // iterate: pass/fail
            foreach (var testCase in testCases.Where(i => !i.Inconclusive))
            {
                DoUpdateTestResults(testCase);
            }
            // iterate: inconclusive
            foreach (var testCase in testCases.Where(i => i.Inconclusive))
            {
                DoUpdateTestResults(testCase);
            }

            // test plan
            AttachToTestPlan(testRun);

            // close
            jiraClient.CreateTransition(idOrKey: testRun.Key, transition: "Done", resolution: string.Empty);
        }

        private void AttachToTestPlan(RhinoTestRun testRun)
        {
            // attach to plan (if any)
            var contextPlans = testRun.TestCases.SelectMany(i => (List<string>)i.Context["testPlans"]).Distinct();

            // exit conditions
            if (!contextPlans.Any())
            {
                return;
            }

            // get id and key values
            var plans = jiraClient
                .Get(contextPlans)
                .Select(i => (id: $"{i.SelectToken("id")}", key: $"{i.SelectToken("key")}"));

            // attach
            Parallel.ForEach(plans, options, plan
                => xpandClient.AddExecutionToPlan(idAndKey: plan, idExecution: $"{testRun.Context["runtimeid"]}"));
        }
        #endregion

        #region *** Put: Test Run     ***
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

        private static void OnMethodsError(IEnumerable<MethodInfo> methods)
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
            var onTestCase = ((JToken)testCase.Context[nameof(testCase)]).AsJObject();

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
            var onTestCase = ((JToken)testCase.Context[nameof(testCase)]).AsJObject();

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
            var onTestCase = ((JToken)testCase.Context[nameof(testCase)]).AsJObject();
            var preconditions = xpandClient.GetPreconditionsByTest($"{onTestCase["id"]}", $"{onTestCase["key"]}");

            // exit conditions
            if (!preconditions.Any())
            {
                return testCase;
            }

            // get all preconditions as data tables
            var dataTables = jiraClient
                .Get(preconditions)
                .Select(i => new DataTable().FromMarkDown($"{i.SelectToken("fields.description")}".Replace("\\{", "{").Replace("\\[", "[").Trim(), default));

            // put
            testCase.DataSource = dataTables.First().Apply(dataTables.Skip(1)).ToArray();
            return testCase;
        }

        // puts the project key under each RhinoTestCase.Context
        [Pipeline(order: 3)]
        private IEnumerable<RhinoTestCase> SetProjectKey(IEnumerable<RhinoTestCase> testCases)
        {
            // iterate
            foreach (var testCase in testCases)
            {
                testCase.Context["projectKey"] = $"{jiraClient.ProjectMeta.SelectToken("key")}";
            }

            // get
            return testCases;
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
            return bugsManager.GetBugs(testCase);
        }

        /// <summary>
        /// Asserts if the RhinoTestCase has already an open bug.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to assert against match bugs.</param>
        /// <returns>An open bug.</returns>
        public override string GetOpenBug(RhinoTestCase testCase)
        {
            return bugsManager.GetOpenBug(testCase);
        }

        /// <summary>
        /// Creates a new bug under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider bug.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public override string OnCreateBug(RhinoTestCase testCase)
        {
            return bugsManager.OnCreateBug(testCase);
        }

        /// <summary>
        /// Updates an existing bug (partial updates are supported, i.e. you can submit and update specific fields only).
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to update automation provider bug.</param>
        public override string OnUpdateBug(RhinoTestCase testCase)
        {
            return bugsManager.OnUpdateBug(testCase, "Done", string.Empty); // status and resolution apply here only for duplicates.
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override IEnumerable<string> OnCloseBugs(RhinoTestCase testCase)
        {
            return bugsManager.OnCloseBugs(testCase, "Done", string.Empty);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public override string OnCloseBug(RhinoTestCase testCase)
        {
            return bugsManager.OnCloseBug(testCase, "Done", string.Empty);
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

            // update
            try
            {
                DoUpdateTestResults(testCase, project, execution, run, steps);
            }
            catch (Exception e) when (e != null)
            {
                logger?.Error($"Update-TestResults -Execution [{execution}] = false", e);
            }
        }

        private void DoUpdateTestResults(
            RhinoTestCase testCase,
            string project,
            string execution,
            string run,
            IEnumerable<string> steps)
        {
            // exit conditions
            if (!testCase.Context.ContainsKey("outcome") || $"{testCase.Context["outcome"]}".Equals("EXECUTING", Compare))
            {
                xpandClient.UpdateTestRunStatus((execution, testCase.TestRunKey), project, run, "EXECUTING");
                logger.Trace($"Get-TestStatus -Key [{testCase.Key}] = EXECUTING");
                return;
            }

            // build
            var onSteps = steps.ToArray();
            var testSteps = new List<(string, string)>();
            for (int i = 0; i < onSteps.Length; i++)
            {
                var result = testCase.Steps.ElementAt(i).Actual ? "PASSED" : "FAILED";
                testSteps.Add((onSteps[i], result));
            }

            // apply on steps
            Parallel.ForEach(testSteps, options, testStep
                => xpandClient.UpdateStepStatus((execution, testCase.TestRunKey), run, testStep));

            testCase
                .SetInconclusiveComment()
                .SetEvidences()
                .SetActual()
                .SetFailedComment();

            // apply on test
            var response = xpandClient
                .UpdateTestRunStatus((execution, testCase.TestRunKey), project, run, $"{testCase.Context["outcome"]}");

            // set in context
            testCase.Context["runUpdated"] = response.SelectToken("testIssueId") != null;
        }
    }
}