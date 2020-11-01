/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Extensions;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;

using RhinoUtilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.AtlassianClients.Framework
{
    /// <summary>
    /// Bugs manager componenet common to all Jira connectors.
    /// </summary>
    public class JiraBugsManager
    {
        // members: state
        private readonly JiraClient client;
        private readonly ILogger logger;

        /// <summary>
        /// Creates a new instance JiraBugsManager.
        /// </summary>
        /// <param name="client">JiraClient by which to utilize Jira requests.</param>
        public JiraBugsManager(JiraClient client)
            : this(client, logger: default)
        { }

        /// <summary>
        /// Creates a new instance JiraBugsManager.
        /// </summary>
        /// <param name="client">JiraClient by which to utilize Jira requests.</param>
        /// <param name="logger">Logger implementation for this JiraClient</param>
        public JiraBugsManager(JiraClient client, ILogger logger)
        {
            this.client = client;
            this.logger = logger != default ? logger.CreateChildLogger(nameof(JiraBugsManager)) : logger;
        }

        /// <summary>
        /// Gets a list of open bugs.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to find bugs.</param>
        /// <returns>A list of bugs (can be JSON or ID for instance).</returns>
        public IEnumerable<string> GetBugs(RhinoTestCase testCase)
        {
            return DoGetBugs(testCase).Select(i => $"{i}");
        }

        /// <summary>
        /// Asserts if the RhinoTestCase has already an open bug.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to assert against match bugs.</param>
        /// <returns>An open bug.</returns>
        public string GetOpenBug(RhinoTestCase testCase)
        {
            // setup
            var bugs = DoGetBugs(testCase);

            // get
            var openBug = bugs.Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false));

            // assert
            var onBug = openBug.Any() ? openBug.First() : JToken.Parse("{}");

            // get
            return GetOpenBug(testCase, $"{onBug.SelectToken("key")}");
        }

        /// <summary>
        /// Creates a new bug under the specified automation provider.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to create automation provider bug.</param>
        /// <returns>The ID of the newly created entity.</returns>
        public string OnCreateBug(RhinoTestCase testCase)
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
        public string OnUpdateBug(RhinoTestCase testCase, string status, string resolution)
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
                var issues = client.Get(idsOrKeys: bugs).Where(i => testCase.IsBugMatch(bug: i, assertDataSource: true));

                var onBugs = issues
                    .OrderBy(i => $"{i["key"]}")
                    .Skip(1)
                    .Select(i => $"{i.SelectToken("key")}")
                    .Where(i => !string.IsNullOrEmpty(i));

                var labels = new[] { "Duplicate" };

                DoCloseBugs(testCase, status, resolution: !string.IsNullOrEmpty(resolution) ? "Duplicate" : string.Empty, labels, bugs: onBugs);
            }

            // update
            bugs = client
                .Get(idsOrKeys: bugs)
                .Select(i => i.AsJObject())
                .Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false))
                .Select(i => $"{i.SelectToken("key")}")
                .Where(i => !string.IsNullOrEmpty(i));

            testCase.UpdateBug(idOrKey: bugs.FirstOrDefault(), client);

            // get
            return $"{Utilities.GetUrl(client.Authentication.Collection)}/browse/{bugs.FirstOrDefault()}";
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public IEnumerable<string> OnCloseBugs(RhinoTestCase testCase, string status, string resolution)
        {
            // get existing bugs
            var bugs = DoGetBugs(testCase).Select(i => $"{i.SelectToken("key")}").Where(i => !string.IsNullOrEmpty(i));

            // close bugs
            return DoCloseBugs(testCase, status, resolution, Array.Empty<string>(), bugs);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public IEnumerable<string> OnCloseBugs(RhinoTestCase testCase, string status, string resolution, IEnumerable<string> bugs)
        {
            // set existing bugs
            testCase.Context["bugs"] = bugs;

            // close bugs
            return DoCloseBugs(testCase, status, resolution, Array.Empty<string>(), bugs);
        }

        /// <summary>
        /// Close all existing bugs.
        /// </summary>
        /// <param name="testCase">Rhino.Api.Contracts.AutomationProvider.RhinoTestCase by which to close automation provider bugs.</param>
        public string OnCloseBug(RhinoTestCase testCase, string status, string resolution)
        {
            // get existing bugs
            var isBugs = testCase.Context.ContainsKey("bugs") && testCase.Context["bugs"] != default;
            var contextBugs = isBugs ? (IEnumerable<string>)testCase.Context["bugs"] : Array.Empty<string>();
            var bugs = client
                .Get(idsOrKeys: contextBugs)
                .Where(i => testCase.IsBugMatch(bug: i, assertDataSource: false));

            // get conditions (double check for bugs)
            if (!bugs.Any())
            {
                return string.Empty;
            }

            // close bugs: first
            var onBug = $"{bugs.FirstOrDefault()?.SelectToken("key")}";
            testCase.CloseBug(bugIssueKey: onBug, status, resolution, client);

            // close bugs: duplicate (if any)
            foreach (var bug in bugs.Skip(1))
            {
                var labels = new[] { "Duplicate" };
                testCase.CloseBug(
                    $"{bug.SelectToken("key")}",
                    status,
                    resolution: !string.IsNullOrEmpty(resolution) ? "Duplicate" : string.Empty,
                    labels,
                    client);
            }
            return onBug;
        }

        private IEnumerable<string> DoCloseBugs(RhinoTestCase testCase, string status, string resolution, IEnumerable<string> labels, IEnumerable<string> bugs)
        {
            // close bugs
            var closedBugs = new List<string>();
            foreach (var bug in bugs)
            {
                var isClosed = testCase.CloseBug(bugIssueKey: bug, status, resolution, labels, client);

                // logs
                if (isClosed)
                {
                    closedBugs.Add($"{Utilities.GetUrl(client.Authentication.Collection)}/browse/{bug}");
                    continue;
                }
                logger?.Info($"Close-Bug -Bug [{bug}] -Test [{testCase.Key}] = false");
            }

            // context
            if (!testCase.Context.ContainsKey(ContextEntry.BugClosed) || !(testCase.Context[ContextEntry.BugClosed] is IEnumerable<string>))
            {
                testCase.Context[ContextEntry.BugClosed] = new List<string>();
            }
            var onBugsClosed = (testCase.Context[ContextEntry.BugClosed] as IEnumerable<string>).ToList();
            onBugsClosed.AddRange(closedBugs);
            testCase.Context[ContextEntry.BugClosed] = onBugsClosed;
            return onBugsClosed;
        }

        private IEnumerable<JToken> DoGetBugs(RhinoTestCase testCase)
        {
            // shortcuts
            var bugType = testCase.GetCapability(AtlassianCapabilities.BugType, "Bug");
            const string typePath = "fields.issuetype.name";
            const string statusPath = "fields.status.name";

            // get test issue
            var test = client.Get(testCase.Key).AsJObject();

            // get bugs
            var bugsKeys = test
                .SelectTokens("..inwardIssue")
                .Where(i => $"{i.SelectToken(typePath)}"?.Equals(bugType) == true && $"{i.SelectToken(statusPath)}"?.Equals("Closed") != true)
                .Select(i => $"{i["key"]}")
                .ToArray();

            // add to context
            testCase.Context["bugs"] = bugsKeys;

            // get issues
            var bugs = client.Get(bugsKeys);
            testCase.Context["bugsData"] = bugs;

            // get
            return bugs;
        }

        private string DoCreateBug(RhinoTestCase testCase)
        {
            // get bug response
            var response = testCase.CreateBug(client);

            // results
            return response == default
                ? "-1"
                : $"{Utilities.GetUrl(client.Authentication.Collection)}/browse/{response["key"]}";
        }

        private string GetOpenBug(RhinoTestCase testCase, string bugIssueKey)
        {
            // exit conditions
            if (string.IsNullOrEmpty(bugIssueKey))
            {
                return string.Empty;
            }

            // constants
            const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

            // setup conditions
            var isBugsData = testCase.Context.ContainsKey("bugsData") && testCase.Context["bugsData"] != default;

            // setup
            var bugs = isBugsData ? (IEnumerable<JToken>)testCase.Context["bugsData"] : Array.Empty<JToken>();

            // get
            if (!bugs.Any())
            {
                return string.Empty;
            }

            // assert: any
            var onBug = bugs.FirstOrDefault(i => $"{i.SelectToken("key")}".Equals(bugIssueKey, Compare));
            if (onBug == default)
            {
                return string.Empty;
            }

            // assert: status
            var isDoneStatus = $"{onBug.SelectToken("fields.status.name")}".Equals("Done", Compare);
            var isClosedStatus = $"{onBug.SelectToken("fields.status.name")}".Equals("Done", Compare);

            // get
            return !(isDoneStatus || isClosedStatus) ? $"{onBug}" : string.Empty;
        }
    }
}