// 0hshf1gBkfZqsoABp9oO173D
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Rhino.Api;
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
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
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
            // setup
            var testCases = new ConcurrentBag<RhinoTestCase>();

            // iterate - one by one on debug, parallel on production
            foreach (var issueKeys in ids.Split(bucketSize))
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = bucketSize
                };
                Parallel.ForEach(issueKeys, options, key => testCases.AddRange(GetTests(key)));
            }
            return testCases;
        }

        private IEnumerable<RhinoTestCase> GetTests(string issueKey)
        {
            // get issue type
            var issueType = jiraClient.GetIssueType(issueKey);
            var capability = string.Empty;
            var typeEntry = Configuration.ProviderConfiguration.Capabilities.Where(i => $"{i.Value}".Equals(issueType, Compare));
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
                var message =
                    $"Tests were not loaded. Was not able to find execution method for [{issueKey}] issue type.";
                logger?.Error(message);
                return Array.Empty<RhinoTestCase>();
            }

            // invoke and return results
            return method.Invoke(this, new object[] { issueKey }) as IEnumerable<RhinoTestCase>;
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

        private RhinoTestCase DoGetByTest(string issueKey)
        {
            // parse into JObject
            var jsonObject = xpandClient.GetTestCase(issueKey);

            // parse into connector test case
            var test = jsonObject == default ? new RhinoTestCase { Key = "-1" } : jsonObject.ToRhinoTestCase();
            if (test.Key.Equals("-1"))
            {
                return test;
            }

            //// setup project key
            //test.Context["projectKey"] = $"{jsonObject.SelectToken("fields.project.key")}";

            //// load test set (if available - will take the )
            //var customField = jiraClient.GetCustomField(TestCaseSchema);
            //var testSet = jsonObject.SelectToken($"..{customField}");
            //if (testSet.Any())
            //{
            //    test.TestSuite = $"{testSet.First}";
            //}

            //// load test-plans if any
            //customField = jiraClient.GetCustomField(AssociatedPlanSchema);
            //var testPlans = jsonObject.SelectToken($"..{customField}");
            //var onTestPlans = new List<string>();
            //foreach (var testPlan in testPlans)
            //{
            //    onTestPlans.Add($"{testPlan}");
            //}
            //test.Context["testPlans"] = onTestPlans.Count > 0 ? onTestPlans : new List<string>();

            //// load data-sources (multiple preconditions data loading)
            //customField = jiraClient.GetCustomField(PreconditionSchema);
            //var preconditions = jsonObject.SelectToken($"..{customField}");
            //if (!preconditions.Any())
            //{
            //    return test;
            //}

            //// load preconditions
            //var mergedDataSource = preconditions
            //    .Select(i => new DataTable().FromMarkDown($"{jiraClient.GetIssue($"{i}").SelectToken("fields.description")}".Trim(), default))
            //    .Merge();
            //test.DataSource = mergedDataSource.ToDictionary().Cast<Dictionary<string, object>>().ToArray();

            // return populated test
            return test;
        }
        #endregion
    }
}