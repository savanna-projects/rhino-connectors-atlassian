/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Connectors.AtlassianClients.Contracts;

using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Rhino.Connectors.Xray.Cloud.Framework
{
    /// <summary>
    /// Holds various, ready to send or ready to factor commands
    /// </summary>
    internal static class XpandCommandsRepository
    {
        // constants
        public const string XpandPath = "https://xray.cloud.xpand-it.com";

        #region *** Get  ***
        /// <summary>
        /// Gets all steps from a test issue.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetSteps((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/test/{0}/steps?startAt=0&maxResults=1000";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets all test issues under a test set.
        /// </summary>
        /// <param name="idAndKey">The test set issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTestsBySet((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/issuelinks/testset/{0}/tests";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets all test issues under a test plan.
        /// </summary>
        /// <param name="idAndKey">The test plan issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTestsByPlan((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/testplan/{0}/tests";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets all test sets under which the provided test case is listed.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetSetsByTest((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/issuelinks/testset/{0}/tests?direction=inward";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets all test plans under which the provided test case is listed.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetPlansByTest((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/issuelinks/testPlan/{0}/tests?direction=inward";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets all precondition issues associated with this test case.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetPreconditionsByTest((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/issuelinks/test/{0}/preConditions";

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Get,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Gets metadata for execution details page. Use the response of this command to get
        /// the execution details route.
        /// </summary>
        /// <param name="executionKey">The test execution key.</param>
        /// <param name="testKey">The test issue key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetExecutionDetailsMeta(string executionKey, string testKey)
        {
            // setup
            const string Format = "/plugins/servlet/ac/com.xpandit.plugins.xray/execution-page" +
                "?classifier=json" +
                "&ac.testExecIssueKey={0}" +
                "&ac.testIssueKey={1}";

            // get
            return new HttpCommand
            {
                Method = HttpMethod.Get,
                Route = string.Format(Format, executionKey, testKey)
            };
        }

        /// <summary>
        /// Gets the execution details of a single test run.
        /// </summary>
        /// <param name="route">Execution details route address.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        /// <remarks>User GetExecutionDetailsMeta to get the route for this request.</remarks>
        public static HttpCommand GetExecutionDetails(string route)
        {
            // setup
            var issueKey = Regex.Match(input: route, pattern: @"(?<=testExecIssueKey=)\w+-\d+").Value;

            // get
            return new HttpCommand
            {
                Headers = GetHeaders(issueKey),
                Method = HttpMethod.Get,
                Route = route
            };
        }

        /// <summary>
        /// Gets a collection of test runs under a test execution issue.
        /// </summary>
        /// <param name="idAndKey">The execution issue ID and key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetRunsByExecution((string id, string key) idAndKey)
        {
            // setup
            const string Format = "/api/internal/testruns?testExecIssueId={0}";

            // get
            return new HttpCommand
            {
                Data = new { Fields = new[] { "status", "key" } },
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKey.id)
            };
        }
        #endregion

        #region *** Put  ***

        #endregion

        #region *** Post ***
        /// <summary>
        /// Associate precondition issue to a test case.
        /// </summary>
        /// <param name="idAndKeyTest">The test issue id and key.</param>
        /// <param name="idsPrecondition">A collection of precondition id to add.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddPrecondition((string id, string key) idAndKeyTest, IEnumerable<string> idsPrecondition)
        {
            // setup
            const string Format = "/api/internal/issuelinks/test/{0}/preConditions";

            // get
            return new HttpCommand
            {
                Data = idsPrecondition,
                Headers = GetHeaders(issueKey: idAndKeyTest.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKeyTest.id)
            };
        }

        /// <summary>
        /// Adds tests to a test execution run issue with default status.
        /// </summary>
        /// <param name="idAndKeyExecution">The ID and key of the test execution issue.</param>
        /// <param name="idsTest">A collection of test issue key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddTestToExecution((string id, string key) idAndKeyExecution, IEnumerable<string> idsTest)
        {
            // setup
            const string Format = "/api/internal/issuelinks/testexec/{0}/tests";

            // get
            return new HttpCommand
            {
                Data = idsTest,
                Headers = GetHeaders(issueKey: idAndKeyExecution.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKeyExecution.id)
            };
        }

        /// <summary>
        /// Updates a test run result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="idProject">The ID of the project.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="status">The status to update.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand UpdateTestRunStatus(
            (string id, string key) idAndKey,
            string idProject,
            string run,
            string status)
        {
            // setup
            const string Format = "/api/internal/testrun/{0}/status";
            var data = new Dictionary<string, object>
            {
                ["projectId"] = idProject,
                ["status"] = status.ToUpper()
            };

            // get
            return new HttpCommand
            {
                Data = data,
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, run)
            };
        }

        /// <summary>
        /// Updates test step result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="step">The step ID and status to update.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand UpdateStepStatus(
            (string id, string key) idAndKey,
            string run,
            (string id, string status) step)
        {
            // setup
            const string Format = "/api/internal/testRun/{0}/step/{1}/status";
            var data = new Dictionary<string, object>
            {
                ["status"] = step.status.ToUpper()
            };

            // get
            return new HttpCommand
            {
                Data = data,
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, run, step.id)
            };
        }

        /// <summary>
        /// Adds a test step to an existing test issue.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test issue.</param>
        /// <param name="action">The step action.</param>
        /// <param name="result">The step expected result.</param>
        /// <param name="index">The step order in the test case steps collection.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand CreateTestStep((string id, string key) idAndKey, string action, string result, int index)
        {
            // setup
            const string Format = "/api/internal/test/{0}/step";

            // get
            return new HttpCommand
            {
                Data = new { action, result, index },
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Adds a test execution to an existing test plan.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test plan issue.</param>
        /// <param name="idExecution">The ID of the test execution issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddExecutionToPlan((string id, string key) idAndKey, string idExecution)
        {
            // setup
            const string Format = "/api/internal/testplan/{0}/addTestExecs";

            // get
            return new HttpCommand
            {
                Data = new[] { idExecution },
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Adds a collection of test issue to an existing test set.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test set issue.</param>
        /// <param name="idsTests">A collection of test issue is to add.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddTestsToSet((string id, string key) idAndKey, IEnumerable<string> idsTests)
        {
            // setup
            const string Format = "/api/internal/issuelinks/testset/{0}/tests";

            // get
            return new HttpCommand
            {
                Data = idsTests,
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKey.id)
            };
        }

        /// <summary>
        /// Sets a comment on test execution.
        /// </summary>
        /// <param name="idAndKey">The internal runtime ID and key of the test set issue.</param>
        /// <param name="comment">The comment to set</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand SetCommentOnExecution((string id, string key) idAndKey, string comment)
        {
            // setup
            const string Format = "/api/internal/testRun/{0}/comment";

            // get
            return new HttpCommand
            {
                Data = new { Comment = comment },
                Headers = GetHeaders(issueKey: idAndKey.key),
                Method = HttpMethod.Post,
                Route = string.Format(Format, idAndKey.id)
            };
        }
        #endregion

        // UTILITIES
        private static IDictionary<string, string> GetHeaders(string issueKey) => new Dictionary<string, string>
        {
            ["X-acpt"] = issueKey
        };
    }
}