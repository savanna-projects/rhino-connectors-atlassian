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
    }
}