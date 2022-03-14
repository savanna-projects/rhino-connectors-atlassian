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
using Rhino.Api.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class MarkdownExtensions
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Parse a collection of key/value pairs into a Jira compliant markdown table.
        /// </summary>
        /// <param name="data">Collection to parse.</param>
        /// <returns>Jira compliant markdown table.</returns>
        public static string ToMarkdown(this IEnumerable<IDictionary<string, object>> data)
        {
            return DoToMarkdown(data);
        }

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
        public static string BugMarkdown(this RhinoTestStep testStep, string index)
        {
            return StepToBugMarkdown(testStep, index);
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
        public static string MarkdownEnvironment(this RhinoTestCase testCase)
        {
            return EnvironmentToBugMarkdown(testCase);
        }

        /// <summary>
        /// Converts a RhinoTestCase into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string MarkdownPlatform(this RhinoTestCase testCase)
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

        /// <summary>
        /// Converts a RhinoTestCase.DataSource into XRay compatible markdown which can be placed in any
        /// description type field.
        /// </summary>
        /// <param name="testCase">RhinoTestCase to convert.</param>
        /// <returns>XRay compatible markdown representation of this RhinoTestCase.</returns>
        public static string MarkdownDataSource(this RhinoTestCase testCase)
        {
            return DataSourceToBugMarkdown(testCase);
        }
        #endregion

        #region *** Object Markdown ***
        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A collection of <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>Jira style table.</returns>
        public static string ToJiraMarkdown(this IEnumerable<IDictionary<string, object>> data)
        {
            return DoToMarkdown(data);
        }

        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A <see cref="IDictionary{TKey, TValue}"/> by which to create table.</param>
        /// <returns>Jira style table.</returns>
        public static string ToJiraMarkdown(this IDictionary<string, object> data)
        {
            return DoToMarkdown(data);
        }

        /// <summary>
        /// Gets a markdown table reflection of the provided map collection.
        /// </summary>
        /// <param name="data">A JSON Object by which to create table.</param>
        /// <returns>Jira style table.</returns>
        public static string ToJiraMarkdown(this JToken data)
        {
            // exit conditions
            if (!data.Children().Any())
            {
                return string.Empty;
            }

            // build header
            var markdown = "||Key||Value||\\r\\n";

            // append rows
            foreach (var item in data.AsJObject())
            {
                markdown += $"|{item.Key}|{item.Value}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }
        #endregion

        #region *** Run Markdown    ***
        /// <summary>
        /// Gets a markdown description of this configuration.
        /// </summary>
        /// <param name="configuration">RhinoConfiguration to parse.</param>
        /// <returns>Markdown description of this configuration</returns>
        public static string GetRunDescription(this RhinoConfiguration configuration)
        {
            throw new NotImplementedException();
        }
        #endregion

        // UTILITIES
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
            var priority = JToken
                .Parse(priorityData)["allowedValues"]
                .FirstOrDefault(i => $"{i["name"]}".Equals(name, Compare) && $"{i["id"]}".Equals(id, Compare));

            // results
            return priority == null ? testCase.Priority : $"{priority.SelectToken("id")}";
        }

        private static string EnvironmentToBugMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // setup
                var driverParams = JObject.Parse(System.Text.Json.JsonSerializer.Serialize(testCase.Context[ContextEntry.DriverParams]));

                // setup conditions
                var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;
                var isCapabilites = driverParams.ContainsKey("capabilities");
                var isMobApp = !isWebApp
                    && isCapabilites
                    && driverParams.SelectToken("capabilities.app") != null;

                // get application
                var onTestCase = testCase.Context.ContainsKey("decomposedTestCase")
                    ? (RhinoTestCase)testCase.Context["decomposedTestCase"]
                    : testCase;

                // get application
                return isMobApp
                    ? $"{driverParams.SelectToken("capabilities.app")}"
                    : ((ActionRule)onTestCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument.Replace(@"""", @"\""");
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        private static string DataSourceToBugMarkdown(RhinoTestCase testCase)
        {
            return testCase.DataSource.Any()
                ? "*Local Data Source*\\r\\n" + DoToMarkdown(testCase.DataSource).Replace(@"""", @"\""")
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
                var markdown = new List<string>();
                for (int i = 0; i < testCase.Steps.Count(); i++)
                {
                    var md = StepToBugMarkdown(testCase.Steps.ElementAt(i), $"{i + 1}");
                    markdown.Add(md);
                }
                var steps = string.Join("\\r\\n\\r\\n", markdown);

                // results
                return header.Replace(@"""", @"\""") + steps;
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
            const string Options = "options";
            const string AppPath = "capabilities.app";
            var driverParams = JObject.Parse(System.Text.Json.JsonSerializer.Serialize(testCase.Context[ContextEntry.DriverParams]));

            // set header
            var header =
                "\r\n----\r\n" +
                "*On Platform*: " + $"{driverParams["driver"]}\r\n" +
                "----\r\n";

            // setup conditions
            var isWebApp = IsWebAppAction(testCase.Steps.First());
            var isCapabilites = driverParams.ContainsKey(Capabilities);
            var isMobApp = !isWebApp && isCapabilites && driverParams.SelectToken(AppPath) != null;
            var isOptions = driverParams.ContainsKey(Options) && driverParams.SelectToken(Options) != null;

            // get application
            var onTestCase = testCase.Context.ContainsKey("decomposedTestCase")
                ? (RhinoTestCase)testCase.Context["decomposedTestCase"]
                : testCase;

            var application = isMobApp
                ? $"{driverParams.SelectToken(AppPath)}"
                : ((ActionRule)onTestCase.Steps.First(IsWebAppAction).Context[ContextEntry.StepAction]).Argument;

            // setup environment
            var environment =
                "*Application Under Test*\r\n" +
                "||Name||Value||\r\n" +
                "|Driver|" + $"{driverParams["driver"]}" + "|\r\n" +
                "|Driver Server|" + $"{driverParams["driverBinaries"]}".Replace(@"\", @"\\") + "|\r\n" +
                "|Application|" + application + "|";

            // setup capabilities
            var capabilites = string.Empty;
            var capabilitesToken = driverParams.SelectToken(Capabilities);
            if (isCapabilites && capabilitesToken != null)
            {
                var data = JsonConvert.DeserializeObject<IDictionary<string, object>>($"{capabilitesToken}");
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                capabilites = data.Count == 0 ? string.Empty : "\r\n*Capabilities*\r\n" + $"\r\n{{code:json}}\r\n{json}\r\n{{code}}";
            }

            // setup driver options
            var options = string.Empty;
            if (isOptions)
            {
                var optionsAsObject =
                    JsonConvert.DeserializeObject<IDictionary<string, object>>($"{driverParams.SelectToken(Options)}");
                var optionsAsStr = JsonConvert.SerializeObject(optionsAsObject, Formatting.Indented);
                const string optionsHeader = "\r\n*Options*\r\n";

                var optionsData = optionsHeader + "{code:json}" + optionsAsStr + "{code}";
                options = optionsAsObject.Count == 0 ? string.Empty : optionsData;
            }

            // results
            var lines = Regex
                .Split(header + environment + capabilites + options, @"((\r)+)?(\n)+((\r)+)?")
                .Where(i => !i.StartsWith("\r") && !i.StartsWith("\n"));
            return string.Join("\\r\\n", lines).Replace(@"""", @"\""");
        }

        private static bool IsWebAppAction(RhinoTestStep testStep)
        {
            return Regex.IsMatch(input: testStep.Action, pattern: "(?i)(go to url|navigate to|open|go to)");
        }

        private static string StepToBugMarkdown(RhinoTestStep testStep, string index)
        {
            // local
            static string GetStepMarkdown(RhinoTestStep testStep, string index)
            {
                // setup
                var action = $"*{index}. " + testStep.Action.Replace("{", "\\\\{") + "*";
                var expected = testStep.HaveExpectedResults()
                    ? testStep.ExpectedResults.ToArray()
                    : Array.Empty<RhinoExpectedResult>();

                // root
                if (!testStep.HaveNestedSteps())
                {
                    if (!testStep.Actual && expected.Length > 0)
                    {
                        var markdown = new List<string>
                        {
                            action
                        };
                        for (int i = 0; i < expected.Length; i++)
                        {
                            var outcome = !expected[i].Actual ? "{panel:bgColor=#ffebe6}" : "{panel:bgColor=#e3fcef}";
                            markdown.Add(outcome + expected[i].ExpectedResult.Replace("{", "\\\\{") + "{panel}");
                        }
                        return string.Join("\\r\\n", markdown);
                    }
                    if (!testStep.Actual && expected.Length == 0)
                    {
                        return "{panel:bgColor=#ffebe6}" + action + "{panel}";
                    }
                    return action;
                }

                // nested
                var markdowns = new List<string>
                {
                    $"*{index}. " + Regex.Match(action, @"(?<=\{).*(?=\})").Value.Trim() + "*"
                };
                for (int i = 0; i < testStep.Steps.Count(); i++)
                {
                    var markdown = GetStepMarkdown(testStep.Steps.ElementAt(i), index + $".{i + 1}");
                    markdowns.Add(markdown);
                }

                // get
                return string.Join("\\r\\n\\r\\n", markdowns);
            }

            // iterate
            return GetStepMarkdown(testStep, index);
        }

        private static string DoToMarkdown(this IEnumerable<IDictionary<string, object>> data)
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

            // convert data
            var jsonData = data.Select(i => System.Text.Json.JsonSerializer.Serialize(i));
            data = jsonData.Select(i => JsonConvert.DeserializeObject<IDictionary<string, object>>(i));

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

        private static string DoToMarkdown(IDictionary<string, object> data)
        {
            // exit conditions
            if (data.Keys.Count == 0)
            {
                return string.Empty;
            }

            // convert data
            var jsonData = System.Text.Json.JsonSerializer.Serialize(data);
            data = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonData);

            // build header
            var markdown = "||Key||Value||\\r\\n";

            // append rows
            foreach (var item in data)
            {
                var value = string.IsNullOrEmpty($"{item.Value}") ? " " : $"{item.Value}";
                markdown += $"|{item.Key}|{value}|\\r\\n";
            }

            // results
            return markdown.Trim();
        }
    }
}
