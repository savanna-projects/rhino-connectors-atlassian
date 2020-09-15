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
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.Xray.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.Xray.Extensions
{
    /// <summary>
    /// Extension package for converting Rhino entities into XRay compatible markdown.
    /// </summary>
    public static class MarkdownExtensions
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        #region *** Bug Markdown    ***
        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <param name="jiraClient">JiraClient instance by which to fetch bug information.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string BugMarkdown(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            // setup
            var priority = PriorityToBugMarkdown(testCase, jiraClient);
            var environment = EnvironmentToBugMarkdown(testCase);
            var platform = PlatformToBugMarkdown(testCase);
            var dataSource = DataSourceToBugMarkdown(testCase);
            var description = DescriptionToBugMarkdown(testCase) + "\\n\\r" + platform + "\\n\\r" + dataSource;

            // load JSON body
            return Assembly.GetExecutingAssembly().ReadEmbeddedResource("create_bug_for_test_jira.txt")
                .Replace("[project-key]", $"{testCase.Context["projectKey"]}")
                .Replace("[test-scenario]", testCase.Scenario)
                .Replace("[test-priority]", priority)
                .Replace("[test-actions]", description)
                .Replace("[test-environment]", environment)
                .Replace("[test-id]", testCase.Key);
        }

        /// <summary>
        /// Converts a RhinoTestStep into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testStep">RhinoTestStep to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestStep.</returns>
        public static string BugMarkdown(this RhinoTestStep testStep)
        {
            return StepToBugMarkdown(testStep);
        }

        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <param name="jiraClient">JiraClient instance by which to fetch bug information.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string BugMarkdownPriority(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            return PriorityToBugMarkdown(testCase, jiraClient);
        }

        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string BugMarkdownEnvironment(this RhinoTestCase testCase)
        {
            return EnvironmentToBugMarkdown(testCase);
        }

        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string BugMarkdownPlatform(this RhinoTestCase testCase)
        {
            return PlatformToBugMarkdown(testCase);
        }

        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string BugMarkdownDescription(this RhinoTestCase testCase)
        {
            return DescriptionToBugMarkdown(testCase);
        }
        #endregion

        #region *** Object Markdown ***
        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A collection of <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>XRay style table.</returns>
        public static string ToXrayMarkdown(this IEnumerable<IDictionary<string, object>> data)
        {
            return DictionariesToMarkdown(data);
        }

        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>XRay style table.</returns>
        public static string ToXrayMarkdown(this IDictionary<string, object> data)
        {
            return DictionaryToMarkdown(data);
        }

        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A JSON Object by which to create table.</param>
        /// <returns>XRay style table.</returns>
        public static string ToXrayMarkdown(this JObject data)
        {
            // exit conditions
            if (!data.Children().Any())
            {
                return string.Empty;
            }

            // build header
            var markdown = "||Key||Value||\\r\\n";

            // append rows
            foreach (var item in data)
            {
                markdown += $"|{item.Key}|{item.Value}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }
        #endregion

        // UTILITIES
        private static string StepToBugMarkdown(RhinoTestStep testStep)
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

        private static string PriorityToBugMarkdown(RhinoTestCase testCase, JiraClient jiraClient)
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

        private static string EnvironmentToBugMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // setup
                var driverParams = JObject.Parse(JsonConvert.SerializeObject(testCase.Context[ContextEntry.DriverParams]));

                // setup conditions
                var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;
                var isCapabilites = driverParams.ContainsKey("capabilities");
                var isMobApp = !isWebApp
                    && isCapabilites
                    && driverParams.SelectToken("capabilities.app") != null;

                // get application
                return isMobApp
                    ? JsonConvert.SerializeObject($"{driverParams.SelectToken("capabilities.app")}")
                    : ((ActionRule)testCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument;
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        private static string PlatformToBugMarkdown(RhinoTestCase testCase)
        {
            // setup
            const string Capabilities = "capabilities";
            const string AppPath = "capabilities.app";
            var driverParams = JObject.Parse(JsonConvert.SerializeObject(testCase.Context[ContextEntry.DriverParams]));

            // set header
            var header =
                "\\r\\n----\\r\\n" +
                "*On Platform*: " + $"{driverParams["driver"]}\\r\\n" +
                "----\\r\\n";

            // setup conditions
            var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;
            var isCapabilites = driverParams.ContainsKey(Capabilities);
            var isMobApp = !isWebApp && isCapabilites && driverParams.SelectToken(AppPath) != null;

            // get application
            var application = isMobApp
                ? $"{driverParams.SelectToken(AppPath)}"
                : ((ActionRule)testCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument;

            // setup environment
            var environment =
                "*Application Under Test*\\r\\n" +
                "||Name||Value||\\r\\n" +
                "|Driver|" + $"{driverParams["driver"]}" + "|\\r\\n" +
                "|Driver Server|" + $"{driverParams["driverBinaries"]}".Replace(@"\", @"\\") + "|\\r\\n" +
                "|Application|" + application + "|\\r\\n";

            // setup capabilities
            var capabilites = "*Capabilities*\\r\\n{noformat}" + $"{driverParams[Capabilities]}" + "{noformat}";

            // results
            return (header + environment + capabilites).Trim();
        }

        private static string DataSourceToBugMarkdown(RhinoTestCase testCase)
        {
            return testCase.DataSource.Any()
                ? "*Local Data Source*\\r\\n" + DictionariesToMarkdown(testCase.DataSource)
                : string.Empty;
        }

        private static string DescriptionToBugMarkdown(RhinoTestCase testCase)
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
                var steps = string.Join("\\r\\n\\r\\n", testCase.Steps.Select(StepToBugMarkdown));

                // results
                return header + steps + PlatformToBugMarkdown(testCase);
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        private static string DictionariesToMarkdown(IEnumerable<IDictionary<string, object>> data)
        {
            // exit conditions
            if (!data.Any())
            {
                return string.Empty;
            }

            // get columns
            var columns = data.First().Select(i => i.Key);

            // exit conditions
            if (!columns.Any())
            {
                return string.Empty;
            }

            // build header
            var markdown = "||" + string.Join("||", columns) + "||\\r\\n";

            // build rows
            foreach (var dataRow in data)
            {
                markdown += $"|{string.Join("|", dataRow.Select(i => $"{i.Value}"))}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }

        private static string DictionaryToMarkdown(IDictionary<string, object> data)
        {
            // exit conditions
            if (data.Keys.Count == 0)
            {
                return string.Empty;
            }

            // build header
            var markdown = "||Key||Value||\\r\\n";

            // append rows
            foreach (var item in data)
            {
                markdown += $"|{item.Key}|{item.Value}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }
    }
}