/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Contracts.Extensions;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

using RhinoUtilities = Rhino.Api.Extensions.Utilities;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class RhinoExtensions
    {
        // members: constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        #region *** Authentication  ***
        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestCase.Context.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestCase testCase)
        {
            return DoGetAuthentication(testCase.Context);
        }

        /// <summary>
        /// Gets a JiraAuthentication based on the configuration in RhinoTestRun.Context.
        /// </summary>
        /// <param name="testRun">RhinoTestRun to get JiraAuthentication from.</param>
        /// <returns>JiraAuthentication object or empty JiraAuthentication if not found.</returns>
        public static JiraAuthentication GetAuthentication(this RhinoTestRun testRun)
        {
            return DoGetAuthentication(testRun.Context);
        }

        private static JiraAuthentication DoGetAuthentication(IDictionary<string, object> context)
        {
            // exit conditions
            if (!context.ContainsKey(ContextEntry.Configuration))
            {
                return new JiraAuthentication();
            }

            // get
            var configuration = context[ContextEntry.Configuration] as RhinoConfiguration;

            // exit conditions
            if (configuration == default)
            {
                return new JiraAuthentication();
            }

            // get
            return new JiraAuthentication
            {
                AsOsUser = configuration.ConnectorConfiguration.AsOsUser,
                Capabilities = configuration.Capabilities,
                Collection = configuration.ConnectorConfiguration.Collection,
                Password = configuration.ConnectorConfiguration.Password,
                User = configuration.ConnectorConfiguration.User,
                Project = configuration.ConnectorConfiguration.Project
            };
        }
        #endregion

        /// <summary>
        /// Gets a comment text for failed test case which includes meta data information.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to get comment for.</param>
        /// <returns>Jira markdown fail comment.</returns>
        public static string GetFailComment(this RhinoTestCase testCase)
        {
            // setup
            var failedSteps = testCase.Steps.Where(i => !i.Actual).Select(i => ((JToken)i.Context["testStep"])["index"]);

            // exit conditions
            if (!failedSteps.Any())
            {
                return string.Empty;
            }

            // setup
            var environment = testCase.MarkdownEnvironment();
            var platform = testCase.MarkdownPlatform();
            var dataSource = testCase.MarkdownDataSource();

            // build
            var header =
                "----\r\n" +
                $"*{DateTime.UtcNow} UTC* \r\n" +
                $"*Failed On Iteration:* {testCase.Iteration}\r\n" +
                $"*On Steps:* {string.Join(", ", failedSteps)}\r\n" +
                $"*On Application:* {environment}\r\n";

            var body = (platform + dataSource)
                .Replace("\\r\\n", "\n")
                .Replace(@"\""", "\"")
                .Replace("----\r\n", string.Empty);

            // return
            return header + body;
        }

        /// <summary>
        /// Gets a bucket size from RhinoConfiguration capabilities or default value if not exists.
        /// </summary>
        /// <param name="configuration">Configuration to get bucket size from.</param>
        /// <returns>Bucket size.</returns>
        public static int GetBuketSize(this RhinoConfiguration configuration)
        {
            // setup
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // get bucket size value
            if (capabilities?.ContainsKey(ProviderCapability.BucketSize) == false)
            {
                return 15;
            }
            int.TryParse($"{capabilities[ProviderCapability.BucketSize]}", out int bucketSizeOut);

            // return final value
            return bucketSizeOut == 0 ? 15 : bucketSizeOut;
        }

        #region *** Put Issue Types ***
        /// <summary>
        /// Apply issue types into RhinoConfiguration capabilities or default types if needed.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to apply issue types to</param>
        public static void PutDefaultCapabilities(this RhinoConfiguration configuration)
        {
            // setup
            var defaultMap = DefaultTypesMap();
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // factor
            foreach (var key in defaultMap.Keys)
            {
                if (capabilities.ContainsKey(key))
                {
                    continue;
                }
                capabilities[key] = defaultMap[key];
            }
            configuration.Capabilities[options] = capabilities;
        }

        private static IDictionary<string, string> DefaultTypesMap() => new Dictionary<string, string>
        {
            [AtlassianCapabilities.TestType] = "Test",
            [AtlassianCapabilities.SetType] = "Test Set",
            [AtlassianCapabilities.PlanType] = "Test Plan",
            [AtlassianCapabilities.PreconditionsType] = "Precondition",
            [AtlassianCapabilities.ExecutionType] = "Test Execution",
            [AtlassianCapabilities.BugType] = "Bug"
        };
        #endregion

        /// <summary>
        /// Gets a value indicates if this is a dry run (will not create test execution report)
        /// </summary>
        /// <param name="configuration">RhinoConfiguration from which to get capabilities.</param>
        /// <returns><see cref="true"/> for id dryRun; <see cref="false"/> if not.</returns>
        public static bool IsDryRun(this RhinoConfiguration configuration)
        {
            var options = $"{configuration?.ConnectorConfiguration.Connector}:options";
            var capabilities = configuration.Capabilities.ContainsKey(options)
                ? configuration.Capabilities[options] as IDictionary<string, object>
                : new Dictionary<string, object>();

            // setup conditions
            var isKey = capabilities.ContainsKey(AtlassianCapabilities.DryRun);
            var value = isKey ? $"{capabilities[AtlassianCapabilities.DryRun]}" : "false";

            // evaluate
            bool.TryParse(value, out bool dryRunOut);
            return isKey && dryRunOut;
        }

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
                tstCapabilities = tstCapabilities.Substring(0, tstCapabilities.LastIndexOf('|') + 1);

                // extract bug capabilities
                var bugCapabilities = Regex.Match(
                    input: onBug,
                    pattern: @"(?<=Capabilities\W+\\r\\n\|\|).*(?=\|.*Local Data Source)|(?<=Capabilities\W+\\r\\n\|\|).*(?=\|)").Value;

                // normalize to markdown
                var onBugCapabilities = Regex.Split(string.IsNullOrEmpty(bugCapabilities) ? string.Empty : "||" + bugCapabilities + "|", @"\\r\\n");
                bugCapabilities = string.Join(Environment.NewLine, onBugCapabilities);
                bugCapabilities = bugCapabilities.Substring(0, bugCapabilities.LastIndexOf('|') + 1);

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

        #region *** Update: Bug      ***
        /// <summary>
        /// Updates a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to update a bug.</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> if not.</returns>
        public static bool UpdateBug(this RhinoTestCase testCase, string idOrKey, JiraClient jiraClient)
        {
            // setup
            var bugType = testCase.GetCapability(capability: AtlassianCapabilities.BugType, defaultValue: "Bug");
            var onBug = jiraClient.Get(idOrKey).AsJObject();

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
            var comment =
                $"{RhinoUtilities.GetActionSignature("created")} " +
                $"On execution [{testCase.TestRunKey}]";

            // post
            var response = jiraClient.Create(issueBody);
            if (response == default)
            {
                return default;
            }

            // link to test case
            jiraClient.CreateIssueLink(linkType: "Blocks", inward: $"{response["key"]}", outward: testCase.Key, comment);

            // add attachments
            var files = testCase.GetScreenshots();
            jiraClient.AddAttachments($"{response["key"]}", files.ToArray());

            // add to context
            testCase.Context["bugOpenedResponse"] = response;

            // assing
            jiraClient.Assign($"{response["key"]}");

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
        /// <param name="status">The status which will make the bug closed (i.e. Done or Closed).</param>
        /// <param name="resolution">The resolution of the closure.</param>
        /// <param name="labels">A collecction of labels to apply when closing the bug.</param>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static bool CloseBug(
            this RhinoTestCase testCase,
            string bugIssueKey,
            string status,
            string resolution,
            IEnumerable<string> labels,
            JiraClient jiraClient)
        {
            return DoCloseBug(testCase, bugIssueKey, status, resolution, labels, jiraClient);
        }

        /// <summary>
        /// Close a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to close a bug.</param>
        /// <param name="bugIssueKey">The bug issue key to close.</param>
        /// <param name="status">The status which will make the bug closed (i.e. Done or Closed).</param>
        /// <param name="resolution">The resolution of the closure.</param>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static bool CloseBug(
            this RhinoTestCase testCase,
            string bugIssueKey,
            string status,
            string resolution,
            JiraClient jiraClient)
        {
            return DoCloseBug(testCase, bugIssueKey, status, resolution, Array.Empty<string>(), jiraClient);
        }

        /// <summary>
        /// Close a bug based on this RhinoTestCase.
        /// </summary>
        /// <param name="testCase">RhinoTestCase by which to close a bug.</param>
        /// <param name="bugIssueKey">The bug issue key to close.</param>
        /// <param name="status">The status which will make the bug closed (i.e. Done or Closed).</param>
        /// <param name="jiraClient">JiraClient instance to use when closing bug.</param>
        /// <returns><see cref="true"/> if close was successful, <see cref="false"/> if not.</returns>
        public static bool CloseBug(
            this RhinoTestCase testCase,
            string bugIssueKey,
            string status,
            JiraClient jiraClient)
        {
            return DoCloseBug(testCase, bugIssueKey, status, string.Empty, Array.Empty<string>(), jiraClient);
        }

        private static bool DoCloseBug(
            RhinoTestCase testCase,
            string bugIssueKey,
            string status,
            string resolution,
            IEnumerable<string> labels, JiraClient jiraClient)
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

            // send transition
            var transition = jiraClient.CreateTransition(idOrKey: bugIssueKey, status, resolution, string.Empty);
            if (transition)
            {
                var comment = $"{RhinoUtilities.GetActionSignature("closed")} On execution [{testCase.TestRunKey}]";
                jiraClient.AddComment(idOrKey: bugIssueKey, comment);
            }

            // label
            if (transition && labels.Any())
            {
                var data = new { Fields = new { Labels = labels } };
                jiraClient.UpdateIssue(idOrKey: bugIssueKey, data);
            }

            // get
            return transition;
        }
        #endregion
    }
}