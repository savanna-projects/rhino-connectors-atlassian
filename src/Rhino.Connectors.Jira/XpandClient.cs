/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Cloud.Framework;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rhino.Connectors.Xray.Cloud
{
    internal class XpandClient
    {
        // members
        private readonly ILogger logger;
        private readonly JiraClient jiraClient;
        private readonly JiraCommandsExecutor executor;
        private readonly ParallelOptions options;
        private readonly int bucketSize;

        #region *** Constructors       ***
        /// <summary>
        /// Creates a new instance of JiraClient.
        /// </summary>
        /// <param name="authentication">Authentication information by which to connect and fetch data from Jira.</param>
        public XpandClient(JiraAuthentication authentication)
            : this(authentication, logger: default)
        { }

        /// <summary>
        /// Creates a new instance of JiraClient.
        /// </summary>
        /// <param name="authentication">Authentication information by which to connect and fetch data from Jira.</param>
        /// <param name="logger">Logger implementation for this client.</param>
        public XpandClient(JiraAuthentication authentication, ILogger logger)
        {
            // setup
            this.logger = logger?.CreateChildLogger(loggerName: nameof(XpandClient));
            jiraClient = new JiraClient(authentication, this.logger);
            Authentication = jiraClient.Authentication;
            executor = new JiraCommandsExecutor(authentication, this.logger);
            bucketSize = authentication.GetCapability(ProviderCapability.BucketSize, 4);
            options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
        }
        #endregion

        #region *** Properties         ***
        /// <summary>
        /// Jira authentication information.
        /// </summary>
        public JiraAuthentication Authentication { get; }
        #endregion

        #region *** Get: Tests         ***
        /// <summary>
        /// Gets a test cases issue.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <returns>A test case.</returns>
        public JToken GetTestCase(string idOrKey)
        {
            return DoGetTestCases(new[] { idOrKey }).FirstOrDefault();
        }

        /// <summary>
        /// Gets a collection of test cases issues.
        /// </summary>
        /// <param name="idsOrKeys">A collection of ID or key of the issue.</param>
        /// <returns>A collection of test cases.</returns>
        public IEnumerable<JToken> GetTestCases(IEnumerable<string> idsOrKeys)
        {
            return DoGetTestCases(idsOrKeys);
        }

        /// <summary>
        /// Gets a collection of test cases issues from test set.
        /// </summary>
        /// <param name="idsOrKeys">A collection of ID or key of the issue.</param>
        /// <returns>A collection of test cases.</returns>
        public IEnumerable<JToken> GetTestsBySets(IEnumerable<string> idsOrKeys)
        {
            return DoGetByPlanOrSet(byPlans: false, idsOrKeys);
        }

        /// <summary>
        /// Gets a collection of test cases issues from test plans.
        /// </summary>
        /// <param name="idsOrKeys">A collection of ID or key of the issue.</param>
        /// <returns>A collection of test cases.</returns>
        public IEnumerable<JToken> GetTestsByPlans(IEnumerable<string> idsOrKeys)
        {
            return DoGetByPlanOrSet(byPlans: true, idsOrKeys);
        }

        /// <summary>
        /// Gets a collection of test cases issues from test execution.
        /// </summary>
        /// <param name="idsOrKeys">A collection of ID or key of the issue.</param>
        /// <returns>A collection of test cases.</returns>
        public IEnumerable<JToken> GetTestsByExecution(IEnumerable<string> idsOrKeys)
        {
            // setup
            var testCases = new ConcurrentBag<string>();

            // get
            Parallel.ForEach(idsOrKeys, options, idOrKey =>
            {
                var execution = jiraClient.Get(idOrKey).AsJObject();
                var id = $"{execution.SelectToken("id")}";
                var key = $"{execution.SelectToken("key")}";

                var runs = XpandCommandsRepository
                    .GetRunsByExecution((id, key))
                    .Send(executor)
                    .AsJToken()
                    .Select(i => i.AsJObject());

                var range = runs.Select(i => $"{i.SelectToken("testIssueId")}");
                testCases.AddRange(range);
            });

            // get
            return DoGetTestCases(idsOrKeys: testCases);
        }

        // COMMON METHODS
        private IEnumerable<JToken> DoGetByPlanOrSet(bool byPlans, IEnumerable<string> idsOrKeys)
        {
            // setup
            var testSets = JiraCommandsRepository
                .Search(jql: $"key in ({string.Join(",", idsOrKeys)})")
                .Send(executor)
                .AsJToken()
                .AsJObject()
                .SelectToken("issues");

            // exit conditions
            if (testSets?.Any() != true)
            {
                logger?.Warn($"Get-ByPlanOrSet -Keys [{string.Join(",", idsOrKeys)}] = false");
                return Array.Empty<JToken>();
            }

            // get commands list
            var commands = byPlans
                ? testSets.Select(i => XpandCommandsRepository.GetTestsByPlan(($"{i["id"]}", $"{i["key"]}")))
                : testSets.Select(i => XpandCommandsRepository.GetTestsBySet(($"{i["id"]}", $"{i["key"]}")));

            // setup
            var testCases = new ConcurrentBag<string>();
            var token = byPlans ? "issueId" : "id";

            // extract
            Parallel.ForEach(commands, options, command =>
            {
                var ids = command.Send(executor).AsJToken().Select(i => $"{i.SelectToken(token)}");
                testCases.AddRange(ids);
            });

            // get test cases
            return DoGetTestCases(testCases);
        }

        private IEnumerable<JToken> DoGetTestCases(IEnumerable<string> idsOrKeys)
        {
            // get from jira
            var testCases = jiraClient.Get(idsOrKeys);

            // exit conditions
            if (!testCases.Any())
            {
                logger?.Warn($"Get-TestCases -Keys [{string.Join(",", idsOrKeys)}] = false");
                return Array.Empty<JToken>();
            }

            // setup
            var onTestCases = new ConcurrentBag<JToken>();

            // iterate
            Parallel.ForEach(testCases, options, testCase => onTestCases.Add(DoGetTestCase(testCase)));

            // get
            return onTestCases;
        }

        private JToken DoGetTestCase(JToken testCase)
        {
            // get
            var response = XpandCommandsRepository
                .GetSteps(($"{testCase.SelectToken("id")}", $"{testCase.SelectToken("key")}"))
                .Send(executor)
                .AsJToken()
                .AsJObject();

            // setup
            var onTestCase = testCase.AsJObject();
            onTestCase.Add("steps", response.SelectToken("steps"));

            // get
            return onTestCase;
        }
        #endregion

        #region *** Get: Sets          ***
        /// <summary>
        /// Gets all test sets under which the provided test case is listed.
        /// </summary>
        /// <param name="id">The test issue id.</param>
        /// <param name="key">The test issue key.</param>
        /// <returns>A collection of test set id.</returns>
        public IEnumerable<string> GetSetsByTest(string id, string key)
        {
            // get
            var testSets = XpandCommandsRepository.GetSetsByTest((id, key)).Send(executor).AsJToken();

            // parse
            return testSets.Select(i => $"{i.SelectToken("id")}").Where(i => i != default);
        }
        #endregion

        #region *** Get: Plans         ***
        /// <summary>
        /// Gets all test plans under which the provided test case is listed.
        /// </summary>
        /// <param name="id">The test issue id.</param>
        /// <param name="key">The test issue key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public IEnumerable<string> GetPlansByTest(string id, string key)
        {
            // get
            var testPlans = XpandCommandsRepository.GetPlansByTest((id, key)).Send(executor).AsJToken();

            // parse
            return testPlans.Select(i => $"{i.SelectToken("id")}").Where(i => i != default).ToList();
        }
        #endregion

        #region *** Get: Preconditions ***
        /// <summary>
        /// Gets all test plans under which the provided test case is listed.
        /// </summary>
        /// <param name="id">The test issue id.</param>
        /// <param name="key">The test issue key.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public IEnumerable<string> GetPreconditionsByTest(string id, string key)
        {
            // get
            var preconditions = XpandCommandsRepository.GetPreconditionsByTest((id, key)).Send(executor).AsJToken();

            // parse
            return preconditions.Select(i => $"{i.SelectToken("id")}").Where(i => i != default);
        }
        #endregion

        #region *** Get: Execution     ***
        /// <summary>
        /// Gets execution details of test run.
        /// </summary>
        /// <param name="execution">The ID or key of the test execution issue.</param>
        /// <param name="test">The ID or key of the test issue.</param>
        /// <returns>Test execution details.</returns>
        public JToken GetExecutionDetails(string execution, string test)
        {
            // setup
            execution = execution.ToUpper();
            test = test.ToUpper();

            // get
            var issues = jiraClient.Get(new[] { execution, test });

            // setup
            var onExecution = issues.FirstOrDefault(i => $"{i.SelectToken("id")}" == execution || $"{i.SelectToken("key")}" == execution);
            var onTest = issues.FirstOrDefault(i => $"{i.SelectToken("id")}" == test || $"{i.SelectToken("key")}" == test);

            // setup conditions
            var isExecution = onExecution != default;
            var isTest = onTest != default;

            // exit conditions
            if (!isTest || !isExecution)
            {
                logger?.Fatal($"Get-ExecutionDetails -Execution [{execution}] -Test [{test}] = false");
                return JToken.Parse("{}");
            }

            // setup            
            var executionKey = $"{onExecution["key"]}";
            var testKey = $"{onTest["key"]}";
            var route = GetExecutionDetailsRoute(executionKey, testKey);

            // parse 
            var response = XpandCommandsRepository.GetExecutionDetails(route).Send(executor);
            var json = Regex.Match(input: $"{response}", pattern: "(?<=id=\"test-run\" value=\")[^\"]*").Value.Replace("&quot;", "\"");

            // results
            return string.IsNullOrEmpty(json) ? JToken.Parse("{}") : json.AsJToken();
        }

        private string GetExecutionDetailsRoute(string executionKey, string testKey)
        {
            // send
            var response = XpandCommandsRepository
                .GetExecutionDetailsMeta(executionKey, testKey)
                .Send(executor)
                .AsJToken()
                .SelectToken("url");

            // get
            return $"{response}"
                .Replace(XpandCommandsRepository.XpandBaseUrl, string.Empty)
                .Replace("https://xray.cloud.getxray.app", string.Empty);
        }

        /// <summary>
        /// Gets a collection of test runs under a test execution issue.
        /// </summary>
        /// <param name="idAndKey">The execution issue ID and key.</param>
        /// <returns>A collection of test runs (id and status).</returns>
        public JToken GetRunsByExecution((string id, string key) idAndKey)
        {
            return XpandCommandsRepository.GetRunsByExecution(idAndKey).Send(executor);
        }
        #endregion

        #region *** Put: Test          ***
        /// <summary>
        /// Associate precondition issue to a test case.
        /// </summary>
        /// <param name="precondition">The ID or key of the precondition issue.</param>
        /// <param name="test">The ID or key of the test issue.</param>
        public void AddPrecondition(string precondition, string test)
        {
            // setup
            precondition = precondition.ToUpper();
            test = test.ToUpper();

            // get
            var issues = jiraClient.Get(new[] { precondition, test });

            // setup
            var onPrecondition = issues.FirstOrDefault(i => $"{i.SelectToken("id")}" == precondition || $"{i.SelectToken("key")}" == precondition);
            var onTest = issues.FirstOrDefault(i => $"{i.SelectToken("id")}" == test || $"{i.SelectToken("key")}" == test);

            // setup conditions
            var isPrecondition = onPrecondition != default;
            var isTest = onTest != default;

            // exit conditions
            if (!isTest || !isPrecondition)
            {
                logger?.Fatal($"Add-Precondition -Precondition [{precondition}] -Test [{test}] = false");
                return;
            }

            // setup
            var id = $"{onTest.SelectToken("id")}";
            var key = $"{onTest.SelectToken("key")}";
            var preconditions = onPrecondition.SelectTokens("id").Cast<string>();

            // set
            XpandCommandsRepository.AddPrecondition((id, key), preconditions).Send(executor);
        }
        #endregion

        #region *** Put: Execution     ***
        /// <summary>
        /// Adds tests to a test execution run issue with default status.
        /// </summary>
        /// <param name="idOrKeyExecution">The ID or key of the test execution issue.</param>
        /// <param name="idsOrKeysTest">A collection of test issue ID or key.</param>
        public void AddTestsToExecution(string idOrKeyExecution, IEnumerable<string> idsOrKeysTest)
        {
            // setup
            var batches = idsOrKeysTest.Split(49);

            // put
            Parallel.ForEach(batches, options, batch => AddTests(idOrKeyExecution, idsOrKeysTest: batch));
        }

        // add tests bucket to test execution
        private void AddTests(string idOrKeyExecution, IEnumerable<string> idsOrKeysTest)
        {
            // setup: execution
            var onExecution = jiraClient.Get(idOrKeyExecution).AsJObject();
            var id = $"{onExecution.SelectToken("id")}";
            var key = $"{onExecution.SelectToken("key")}";

            // exit conditions
            if (string.IsNullOrEmpty(id))
            {
                logger?.Error($"Was not able to find execution [{idOrKeyExecution}]");
                return;
            }

            // setup: tests to add
            var testCases = jiraClient.Search(jql: $"key in ({string.Join(",", idsOrKeysTest)})")
                .Select(i => $"{i.SelectToken("id")}")
                .Where(i => i != default)
                .Distinct();

            // send
            XpandCommandsRepository.AddTestToExecution((id, key), idsTest: testCases).Send(executor);
        }

        /// <summary>
        /// Updates a test run result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="idProject">The ID of the project.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="status">The status to update.</param>
        public JToken UpdateTestRunStatus(
            (string id, string key) idAndKey,
            string idProject,
            string run,
            string status)
        {
            return XpandCommandsRepository.UpdateTestRunStatus(idAndKey, idProject, run, status).Send(executor).AsJToken();
        }

        /// <summary>
        /// Updates test step result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="step">The step ID and status to update.</param>
        public void UpdateStepStatus((string id, string key) idAndKey, string run, (string id, string key) step)
        {
            XpandCommandsRepository.UpdateStepStatus(idAndKey, run, step).Send(executor);
        }

        /// <summary>
        /// Updates test step actual result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="step">The step ID and result to update.</param>
        public void UpdateStepActual(
            (string id, string key) idAndKey,
            string run,
            (string id, string actual) step)
        {
            XpandCommandsRepository.UpdateStepActual(idAndKey, run, step).Send(executor);
        }

        /// <summary>
        /// Adds a test execution to an existing test plan.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test plan issue.</param>
        /// <param name="idExecution">The ID of the test execution issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken AddExecutionToPlan((string id, string key) idAndKey, string idExecution)
        {
            return XpandCommandsRepository.AddExecutionToPlan(idAndKey, idExecution).Send(executor).AsJToken();
        }

        /// <summary>
        /// Adds a collection of test issue to an existing test set.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test set issue.</param>
        /// <param name="idsTests">A collection of test issue is to add.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken AddTestsToSet((string id, string key) idAndKey, IEnumerable<string> idsTests)
        {
            return XpandCommandsRepository.AddTestsToSet(idAndKey, idsTests).Send(executor).AsJToken();
        }

        /// <summary>
        /// Sets a comment on test execution.
        /// </summary>
        /// <param name="idAndKey">The internal runtime ID and key of the test set issue.</param>
        /// <param name="comment">The comment to set</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken SetCommentOnExecution((string id, string key) idAndKey, string comment)
        {
            return XpandCommandsRepository.SetCommentOnExecution(idAndKey, comment).Send(executor);
        }

        /// <summary>
        /// Adds an existing defect to an existing execution.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the bug issue.</param>
        /// <param name="idExecution">The internal runtime id of the excution.</param>
        public JToken AddDefectToExecution((string id, string key) idAndKey, string idExecution)
        {
            return XpandCommandsRepository.AddDefectToExecution(idAndKey, idExecution).Send(executor);
        }
        #endregion

        #region *** Post: Test Steps   ***
        /// <summary>
        /// Adds a test step to an existing test issue.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test issue.</param>
        /// <param name="action">The step action.</param>
        /// <param name="result">The step expected result.</param>
        /// <param name="index">The step order in the test case steps collection.</param>
        public void CreateTestStep((string id, string key) idAndKey, string action, string result, int index)
        {
            XpandCommandsRepository.CreateTestStep(idAndKey, action, result, index).Send(executor);
        }

        /// <summary>
        /// Creates an evidence on a test step and test run (the same evidence, linked to both)
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test run issue.</param>
        /// <param name="testRun">The test run internal ID.</param>
        /// <param name="testStep">The test step internal ID.</param>
        /// <param name="file">The file to upload as evidence.</param>
        public void CreateEvidence((string id, string key) idAndKey, string testRun, string testStep, string file)
        {
            // setup: create attachment request (on test run)
            var request = CreateAttachmentRequest(testRun, idAndKey.key, file);

            // send to jira
            var response = JiraCommandsExecutor.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Create-Attachment -Key [{idAndKey.key}] -File [{file}] = false");
                return;
            }

            // setup
            var requestBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // send to XpandIT step
            request = CreateEvidenceRequest(idAndKey.key, testRun, testStep, requestBody);
            response = JiraCommandsExecutor.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Create-Evidence -Key [{idAndKey.key}] -Step [{testStep}] -File [{file}] = false");
            }

            // send to XpandIT run
            request = CreateEvidenceRequest(idAndKey.key, testRun, testStep: string.Empty, requestBody);
            response = JiraCommandsExecutor.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Create-Evidence -Key [{idAndKey.key}] -Run [{testRun}] -File [{file}] = false");
            }
        }

        private HttpRequestMessage CreateAttachmentRequest(string testRun, string key, string file)
        {
            // setup
            var urlPath = $"{XpandCommandsRepository.XpandBaseUrl}/api/internal/attachments?testRunId={testRun}";

            // build request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;
            requestMessage.Headers.Authorization = Authentication.GetAuthenticationHeader();
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            // build multi part content
            var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

            // build file content
            var fileInfo = new FileInfo(file);
            var fileContents = File.ReadAllBytes(fileInfo.FullName);
            var byteArrayContent = new ByteArrayContent(fileContents);
            byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");
            multiPartContent.Add(byteArrayContent, "attachment", fileInfo.Name);

            // set request content
            requestMessage.Content = multiPartContent;
            requestMessage.Headers.Add("X-acpt", jiraClient.GetJwt(key));

            // get
            return requestMessage;
        }

        private HttpRequestMessage CreateEvidenceRequest(string key, string testRun, string testStep, string requestBody)
        {
            // setup
            var endpoint = string.IsNullOrEmpty(testStep)
                ? $"{XpandCommandsRepository.XpandBaseUrl}/api/internal/testrun/{testRun}/evidence"
                : $"{XpandCommandsRepository.XpandBaseUrl}/api/internal/testrun/{testRun}/step/{testStep}/evidence";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var jwt = jiraClient.GetJwt(key);

            // build
            var request = new HttpRequestMessage
            {
                Content = content,
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint)
            };
            request.Headers.ExpectContinue = false;
            request.Headers.Authorization = Authentication.GetAuthenticationHeader();
            request.Headers.Add("X-Atlassian-Token", "no-check");
            request.Headers.Add("X-acpt", jwt);

            // get
            return request;
        }
        #endregion
    }
}