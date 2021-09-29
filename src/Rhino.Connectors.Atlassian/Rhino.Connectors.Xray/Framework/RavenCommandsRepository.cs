/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Connectors.AtlassianClients.Contracts;

using System.Collections.Generic;
using System.Net.Http;

namespace Rhino.Connectors.Xray.Framework
{
    public static class RavenCommandsRepository
    {
        // constants
        private const string ApiVersion = "2.0";

        #region *** Get  ***
        /// <summary>
        /// Gets test execution results from a test run.
        /// </summary>
        /// <param name="testExecIssueKey">Test execution issue key to get results by.</param>
        /// <param name="testIssueKey">Test issue key to get results by.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTestRunExecutionDetails(string testExecIssueKey, string testIssueKey)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testrun/?testExecIssueKey={0}&testIssueKey={1}";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(Format, testExecIssueKey, testIssueKey)
            };
        }

        /// <summary>
        ///  Gets a list of all Test (Run) Statuses, including the default ones.
        /// </summary>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTestStauses()
        {
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = "/rest/raven/" + ApiVersion + "/api/settings/teststatuses"
            };
        }

        /// <summary>
        ///  Return a JSON with a list of the test associated with the test execution.
        /// </summary>
        /// <param name="testExecIssueKey">Test execution issue key to get results by.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand GetTestsByExecution(string testExecIssueKey)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testexec/{0}/test";

            // get
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = string.Format(Format, testExecIssueKey)
            };
        }
        #endregion

        #region *** Put  ***
        /// <summary>
        /// Updates a test run.
        /// </summary>
        /// <param name="testRun">The ID of the test run (not issue key).</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand UpdateTestRun(string testRun, object data)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testrun/{0}";

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Put,
                Route = string.Format(Format, testRun)
            };
        }
        #endregion

        #region *** Post ***
        /// <summary>
        /// Creates an attachment as evidence on a test step under test run results.
        /// </summary>
        /// <param name="testRun">Test run id under which to create evidence.</param>
        /// <param name="step">Test step id under which to create evidence</param>
        /// <param name="data">Request body object.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand CreateAttachment(string testRun, string step, object data)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testrun/{0}/step/{1}/attachment";

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = string.Format(Format, testRun, step)
            };
        }

        /// <summary>
        /// Sets a test execute results without setting the test steps (inline set)
        /// </summary>
        /// <param name="testExecIssueKey">Test execution issue key to set results by.</param>
        /// <param name="testIssueKey">Test issue key to set results by.</param>
        /// <param name="data">Request body object.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand SetTestExecuteResult(string testExecIssueKey, string testIssueKey, object data)
        {
            // setup
            const string Format = "/rest/raven/1.0/testexec/{0}/execute/{1}";

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = string.Format(Format, testExecIssueKey, testIssueKey)
            };
        }

        /// <summary>
        /// Associate test executions with the test plan.
        /// </summary>
        /// <param name="testPlanKey">key of the test plan.</param>
        /// <param name="testExecIssuesKeys">A collection of test execution issue key to associate.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AssociateExecutions(string testPlanKey, IEnumerable<string> testExecIssuesKeys)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testplan/{0}/testexecution";

            // get
            return new HttpCommand
            {
                Data = new { Add = testExecIssuesKeys },
                Method = HttpMethod.Post,
                Route = string.Format(Format, testPlanKey)
            };
        }

        /// <summary>
        /// Adds an existing defect to an existing execution.
        /// </summary>
        /// <param name="keyBug">The ID of the bug issue.</param>
        /// <param name="idExecution">The internal runtime id of the excution.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public static HttpCommand AddDefectToExecution(string keyBug, string idExecution)
        {
            // setup
            const string Format = "/rest/raven/" + ApiVersion + "/api/testrun/{0}/defect";

            // get
            return new HttpCommand
            {
                Data = new[] { keyBug },
                Method = HttpMethod.Post,
                Route = string.Format(Format, idExecution)
            };
        }

        public static HttpCommand CreateStep(string testId, object data)
        {
            // setup
            const string Format = "/rest/raven/1.0/customFields/createStep?testId={0}";

            // get
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = string.Format(Format, testId)
            };
        }
        #endregion
    }
}