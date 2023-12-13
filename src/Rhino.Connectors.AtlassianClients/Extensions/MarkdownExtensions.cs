/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Services.DataContracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    /// <summary>
    /// Extension method to format data as Markdown.
    /// </summary>
    public static class MarkdownExtensions
    {
        // Represents the string comparison type for use in various methods.
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        #region *** Object Markdown ***
        /// <summary>
        /// Converts a collection of dictionaries to Markdown format as a table.
        /// </summary>
        /// <param name="data">The collection of dictionaries to convert.</param>
        /// <returns>Markdown representation of the collection as a table.</returns>
        public static string ConvertToMarkdown(this IEnumerable<IDictionary<string, object>> data)
        {
            return ConvertToTableMarkdown(data);
        }

        /// <summary>
        /// Converts the IDictionary<string, object> data to Markdown format.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <returns>Markdown-formatted string.</returns>
        public static string ConvertToMarkdown(this IDictionary<string, object> data)
        {
            return ConvertToTableMarkdown(data);
        }

        /// <summary>
        /// Converts the JToken data to Markdown format.
        /// </summary>
        /// <param name="data">The JToken data to convert.</param>
        /// <returns>Markdown-formatted string.</returns>
        public static string ConvertToMarkdown(this JToken data)
        {
            // Check if the JToken has any children.
            if (!data.Children().Any())
            {
                return string.Empty;
            }

            // Initialize the markdown string with table headers.
            var markdown = "||Key||Value||\\r\\n";

            // Iterate through each key-value pair in the JToken and append to the markdown.
            foreach (var item in data.ConvertToJObject())
            {
                markdown += $"|{item.Key}|{item.Value}|\\r\\n";
            }

            // Trim and return the markdown string.
            return markdown.Trim();
        }
        #endregion

        #region *** Bug Markdown    ***
        /// <summary>
        /// Generates Markdown-formatted bug description for a RhinoTestCase.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <returns>Markdown-formatted bug description.</returns>
        public static string NewBugDescriptionMarkdown(this RhinoTestCase testCase)
        {
            return ConvertToBugDescriptionMarkdown(testCase);
        }

        /// <summary>
        /// Generates a new Jira markdown for a bug based on the RhinoTestCase and JiraClient.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <param name="jiraClient">The JiraClient instance to retrieve issue type fields.</param>
        /// <returns>New Jira markdown for a bug.</returns>
        public static string NewBugMarkdown(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            // Retrieve priority, environment, platform, data source, and description
            var priority = ConvertToPriorityMarkdown(testCase, jiraClient);
            var environment = ConvertToEnvironmentMarkdown(testCase);
            var platform = ConvertToPlatformMarkdown(testCase);
            var dataSource = ConvertToDataSourceMarkdown(testCase);
            var description = ConvertToBugDescriptionMarkdown(testCase) + "\n\r" + platform + "\n\r" + dataSource;

            // Create a new JiraIssue instance
            var issue = new JiraIssue
            {
                Fields = new JiraIssue.JiraFields
                {
                    Description = description,
                    Environment = environment,
                    IssueType = new JiraIssue.IssueType
                    {
                        Name = "Bug"
                    },
                    Priority = new JiraIssue.Priority
                    {
                        Id = priority
                    },
                    Project = new JiraIssue.Project
                    {
                        Key = $"{testCase.Context["projectKey"]}"
                    },
                    Summary = testCase.Scenario
                }
            };

            // Serialize the JiraIssue to JSON using camel case property naming policy
            return System.Text.Json.JsonSerializer.Serialize(issue, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Generates new Jira markdown for a bug based on the RhinoTestStep and index.
        /// </summary>
        /// <param name="testStep">The RhinoTestStep instance representing the test step.</param>
        /// <param name="index">The index of the test step.</param>
        /// <returns>New Jira markdown for a bug.</returns>
        public static string NewBugMarkdown(this RhinoTestStep testStep, string index)
        {
            return ConvertToBugMarkdown(testStep, index);
        }

        /// <summary>
        /// Generates Markdown-formatted data source information for a RhinoTestCase.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <returns>Markdown-formatted data source information.</returns>
        public static string NewDataSourceMarkdown(this RhinoTestCase testCase)
        {
            return ConvertToDataSourceMarkdown(testCase);
        }

        /// <summary>
        /// Generates new Jira markdown for an environment based on the RhinoTestCase.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <returns>Markdown-formatted environment information.</returns>
        public static string NewEnvironmentMarkdown(this RhinoTestCase testCase)
        {
            return ConvertToEnvironmentMarkdown(testCase);
        }

        /// <summary>
        /// Generates new Jira markdown for the platform based on the RhinoTestCase.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <returns>Markdown-formatted platform information.</returns>
        public static string NewPlatformMarkdown(this RhinoTestCase testCase)
        {
            return ConvertToPlatformMarkdown(testCase);
        }

        /// <summary>
        /// Generates new Jira markdown for the priority based on the RhinoTestCase and JiraClient.
        /// </summary>
        /// <param name="testCase">The RhinoTestCase instance representing the test case.</param>
        /// <param name="jiraClient">The JiraClient instance for interacting with Jira.</param>
        /// <returns>New Jira markdown for the priority.</returns>
        public static string NewPriorityMarkdown(this RhinoTestCase testCase, JiraClient jiraClient)
        {
            return ConvertToPriorityMarkdown(testCase, jiraClient);
        }
        #endregion

        // Asserts whether the RhinoTestStep represents a web application action.
        private static bool AssertWebAppAction(RhinoTestStep testStep)
        {
            // Use a case-insensitive regular expression to check for web application actions
            return Regex.IsMatch(input: testStep.Action, pattern: "(?i)(go to url|navigate to|open|go to)");
        }

        // Converts a RhinoTestCase to Markdown format for describing the test case.
        private static string ConvertToBugDescriptionMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // Construct the header with metadata information
                var header =
                    "\r\n----\r\n" +
                    "*Last Update: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC*\r\n" +
                    "*On Iteration*: " + $"{testCase.Iteration}\r\n" +
                    "Bug filed on '" + testCase.Scenario + "'\r\n" +
                    "----\r\n";

                // Iterate through each test step and convert to bug markdown
                var markdown = new List<string>();
                for (int i = 0; i < testCase.Steps.Count(); i++)
                {
                    var bugMarkdown = ConvertToBugMarkdown(testCase.Steps.ElementAt(i), $"{i + 1}");
                    markdown.Add(bugMarkdown);
                }

                // Combine the converted steps into a single string
                var steps = string.Join("\r\n\r\n", markdown);

                return header + steps;
            }
            catch (Exception e) when (e != null)
            {
                // Return an empty string if an exception occurs
                return string.Empty;
            }
        }

        // Converts a RhinoTestCase to Markdown format for describing the data source.
        private static string ConvertToDataSourceMarkdown(RhinoTestCase testCase)
        {
            return testCase.DataSource.Any()
                ? "*Local Data Source*\r\n" + ConvertToTableMarkdown(testCase.DataSource)
                : string.Empty;
        }

        // Converts the environment information of a RhinoTestCase to Markdown format.
        private static string ConvertToEnvironmentMarkdown(RhinoTestCase testCase)
        {
            try
            {
                // Serialize the driver parameters from the RhinoTestCase's context
                var json = System.Text.Json.JsonSerializer.Serialize(testCase.Context[ContextEntry.DriverParams]);
                var driverParams = JObject.Parse(json);

                // Check if the test case is for a web application
                var isWebApp = testCase.Steps.First().Command == ActionType.GoToUrl;

                // Check if the driver parameters contain capabilities
                var isCapabilities = driverParams.ContainsKey("capabilities");

                // Check if it's a mobile application
                var isMobApp = !isWebApp
                    && isCapabilities
                    && driverParams.SelectToken("capabilities.app") != null;

                // Get the original test case or decomposed test case
                var onTestCase = testCase.Context.TryGetValue("decomposedTestCase", out object value)
                    ? (RhinoTestCase)value
                    : testCase;

                // Determine the application based on whether it's a mobile app or web app
                return isMobApp
                    ? $"{driverParams.SelectToken("capabilities.app")}"
                    : ((ActionRule)onTestCase.Steps.First(i => i.Command == ActionType.GoToUrl).Context[ContextEntry.StepAction]).Argument.Replace(@"""", @"\""");
            }
            catch (Exception e) when (e != null)
            {
                return string.Empty;
            }
        }

        // Converts the RhinoTestCase to platform-specific markdown representation.
        private static string ConvertToPlatformMarkdown(RhinoTestCase testCase)
        {
            const string Capabilities = "capabilities";
            const string Options = "options";
            const string AppPath = "capabilities.app";

            // Serialize the driver parameters to JSON
            var json = System.Text.Json.JsonSerializer.Serialize(testCase.Context[ContextEntry.DriverParams]);
            var driverParams = JObject.Parse(json);

            // Define the header for the platform-specific markdown
            var header =
                "\r\n----\r\n" +
                "*On Platform*: " + $"{driverParams["driver"]}\r\n" +
                "----\r\n";

            // Check if the test step represents a web application action
            var isWebApp = AssertWebAppAction(testCase.Steps.First());

            // Check if capabilities are present in driver parameters
            var isCapabilites = driverParams.ContainsKey(Capabilities);

            // Check if the test step represents a mobile application action
            var isMobApp = !isWebApp && isCapabilites && driverParams.SelectToken(AppPath) != null;

            // Check if options are present in driver parameters
            var isOptions = driverParams.ContainsKey(Options) && driverParams.SelectToken(Options) != null;

            // Get the decomposed test case if available, otherwise use the original test case
            var onTestCase = testCase.Context.TryGetValue("decomposedTestCase", out object value)
                ? (RhinoTestCase)value
                : testCase;

            // Determine the application based on the type of application action
            var application = isMobApp
                ? $"{driverParams.SelectToken(AppPath)}"
                : ((ActionRule)onTestCase.Steps.First(AssertWebAppAction).Context[ContextEntry.StepAction]).Argument;

            // Build the environment section of the markdown
            var environment =
                "*Application Under Test*\r\n" +
                "||Name||Value||\r\n" +
                "|Driver|" + $"{driverParams["driver"]}" + "|\r\n" +
                "|Driver Server|" + $"{driverParams["driverBinaries"]}" + "|\r\n" +
                "|Application|" + application + "|";

            // Build the capabilities section of the markdown
            var capabilites = string.Empty;
            var capabilitesToken = driverParams.SelectToken(Capabilities);
            if (isCapabilites && capabilitesToken != null)
            {
                var data = JsonConvert.DeserializeObject<IDictionary<string, object>>($"{capabilitesToken}");
                var jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
                capabilites = data.Count == 0
                    ? string.Empty
                    : "\r\n*Capabilities*\r\n" + $"\r\n{{code:json}}\r\n{jsonData}\r\n{{code}}";
            }

            // Build the options section of the markdown
            var options = string.Empty;
            if (isOptions)
            {
                var token = $"{driverParams.SelectToken(Options)}";
                var optionsAsObject = JsonConvert.DeserializeObject<IDictionary<string, object>>(token);
                var optionsAsStr = JsonConvert.SerializeObject(optionsAsObject, Formatting.Indented);
                const string optionsHeader = "\r\n*Options*\r\n";

                var optionsData = optionsHeader + "{code:json}" + optionsAsStr + "{code}";
                options = optionsAsObject.Count == 0 ? string.Empty : optionsData;
            }

            // Split and filter the markdown lines
            var lines = Regex
                .Split(header + environment + capabilites + options, @"((\r)+)?(\n)+((\r)+)?")
                .Where(i => !i.StartsWith("\r") && !i.StartsWith("\n"));

            // Join the lines to form the final markdown
            return string.Join("\r\n", lines);
        }

        // Converts the priority of a RhinoTestCase to Jira priority using JiraClient.
        private static string ConvertToPriorityMarkdown(RhinoTestCase testCase, JiraClient jiraClient)
        {
            // Get the priority data from Jira for the "Bug" issue type and the "fields.priority" field
            var priorityData = jiraClient.GetIssueTypeFields("Bug", "fields.priority");

            // If the priority data is not available, return an empty string
            if (string.IsNullOrEmpty(priorityData))
            {
                return string.Empty;
            }

            // Extract the ID and name components from the RhinoTestCase's priority
            var id = Regex.Match(input: testCase.Priority, @"\d+").Value;
            var name = Regex.Match(input: testCase.Priority, @"(?<=\d+\s+-\s+)\w+").Value;

            // Parse the priority data and find the matching priority
            var priority = JToken
                .Parse(priorityData)["allowedValues"]
                .FirstOrDefault(i => $"{i["name"]}".Equals(name, Compare) && $"{i["id"]}".Equals(id, Compare));

            // If the priority is not found, return the original priority from the RhinoTestCase
            return priority == null ? testCase.Priority : $"{priority.SelectToken("id")}";
        }

        // Converts a RhinoTestStep to Markdown format for describing a bug.
        private static string ConvertToBugMarkdown(RhinoTestStep testStep, string index)
        {
            // Format the action for the bug report
            var action = $"*{index}. " + testStep.Action.Replace("{", "\\{") + "*";

            // Retrieve expected results or an empty array
            var expected = testStep.HaveExpectedResults()
                ? testStep.ExpectedResults.ToArray()
                : [];

            // Check if there are no nested steps
            if (!testStep.HaveNestedSteps())
            {
                // If there are no actual results and expected results, create a bug panel
                if (!testStep.Actual && expected.Length > 0)
                {
                    var markdown = new List<string>
                    {
                        action
                    };
                    for (int i = 0; i < expected.Length; i++)
                    {
                        var outcome = !expected[i].Actual ? "{panel:bgColor=#ffebe6}" : "{panel:bgColor=#e3fcef}";
                        markdown.Add(outcome + expected[i].ExpectedResult.Replace("{", "\\{") + "{panel}");
                    }
                    return string.Join("\r\n", markdown);
                }

                // If there are no actual results and no expected results, create a bug panel
                if (!testStep.Actual && expected.Length == 0)
                {
                    return "{panel:bgColor=#ffebe6}" + action + "{panel}";
                }

                // If there are actual results, return the action
                return action;
            }

            // Handle nested steps
            var markdowns = new List<string>
            {
                // Format the action for nested steps
                $"*{index}. " + Regex.Match(action, @"(?<=\{).*(?=\})").Value.Trim() + "*"
            };
            for (int i = 0; i < testStep.Steps.Count(); i++)
            {
                var markdown = ConvertToBugMarkdown(testStep.Steps.ElementAt(i), index + $".{i + 1}");
                markdowns.Add(markdown);
            }

            return string.Join("\r\n\r\n", markdowns);
        }

        // Formats the provided data as Markdown.
        private static string ConvertToTableMarkdown(IEnumerable<IDictionary<string, object>> table)
        {
            // Check if the data is empty
            if (!table.Any())
            {
                return string.Empty;
            }

            // Extract column names from the first data row
            var columns = table.First().Select(i => i.Key);

            // Check if there are any columns
            if (!columns.Any())
            {
                return string.Empty;
            }

            // Convert data to JSON and then deserialize it back to preserve data types
            var jsonData = table.Select(i => System.Text.Json.JsonSerializer.Serialize(i));
            table = jsonData.Select(JsonConvert.DeserializeObject<IDictionary<string, object>>);

            // Create the Markdown table header
            var markdown = "||" + string.Join("||", columns) + "||\r\n";

            // Populate the Markdown table with data rows
            foreach (var dataRow in table)
            {
                markdown += $"|{string.Join("|", dataRow.Select(i => $"{i.Value}"))}|\r\n";
            }

            // Trim any leading or trailing whitespaces from the generated Markdown before returning
            return markdown.Trim();
        }

        // Formats the provided data as Markdown table.
        private static string ConvertToTableMarkdown(IDictionary<string, object> row)
        {
            // Check if the data is empty
            if (row.Keys.Count == 0)
            {
                return string.Empty;
            }

            // Serialize and deserialize the data to ensure it's in the expected format
            var jsonData = System.Text.Json.JsonSerializer.Serialize(row);
            row = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonData);

            // Initialize the Markdown table with header
            var markdown = "||Key||Value||\r\n";

            // Iterate over each key-value pair in the data
            foreach (var item in row)
            {
                // Convert the value to a string, handle empty values, and add to the Markdown table
                var value = string.IsNullOrEmpty($"{item.Value}") ? " " : $"{item.Value}";
                markdown += $"|{item.Key}|{value}|\r\n";
            }

            // Trim any leading or trailing whitespaces from the generated Markdown before returning
            return markdown.Trim();
        }
    }
}
