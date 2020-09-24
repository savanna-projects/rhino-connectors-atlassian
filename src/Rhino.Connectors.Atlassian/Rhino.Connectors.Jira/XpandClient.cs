/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * TODO: Factor HTTP Client
 * https://stackoverflow.com/questions/51478525/httpclient-this-instance-has-already-started-one-or-more-requests-properties-ca
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Rhino.Connectors.AtlassianClients;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.Xray.Cloud.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rhino.Connectors.Xray.Cloud
{
    internal class XpandClient
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;
        private const string StepsFormat = "/api/internal/test/{0}/steps?startAt=0&maxResults=100";
        private const string SetsFromTestsFormat = "/api/internal/issuelinks/testset/{0}/tests?direction=inward";
        private const string PlansFromTestsFormat = "/api/internal/issuelinks/testPlan/{0}/tests?direction=inward";
        private const string PreconditionsFormat = "/api/internal/issuelinks/test/{0}/preConditions";
        private const string TestsBySetFormat = "/api/internal/issuelinks/testset/{0}/tests";
        private const string TestsByPlanFormat = "/api/internal/testplan/{0}/tests";
        private const string PreconditionToTestCaseFormat = "/api/internal/issuelinks/test/{0}/preConditions";
        private const string TestsToExecutionFormat = "/api/internal/issuelinks/testexec/{0}/tests";
        private const string TestExecutionDetailsFormat = "/view/page/execute-test?testIssueKey={0}&testExecIssueKey={1}&jwt={2}";
        private const string TestRunStepStatusFormat = "/api/internal/testRun/{0}/step/{1}/status";
        private const string TestRunStatusFormat = "/api/internal/testrun/{0}/status";

        // members
        private readonly ILogger logger;
        private readonly JiraClient jiraClient;

        /// <summary>
        /// Gets the JSON serialization settings used by this JiraClient.
        /// </summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        /// <summary>
        /// Gets the HTTP requests media type used by this JiraClient.
        /// </summary>
        public const string MediaType = "application/json";

        #region *** Constructors      ***
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
            jiraClient = new JiraClient(authentication, logger);
            Authentication = jiraClient.Authentication;
        }
        #endregion

        #region *** Properties        ***
        /// <summary>
        /// Jira authentication information.
        /// </summary>
        public JiraAuthentication Authentication { get; }
        #endregion

        #region *** Get Tests         ***
        public JObject GetTestCase(string issueKey)
        {
            return DoGetTestCases(bucketSize: 1, issueKey).FirstOrDefault();
        }

        public IEnumerable<JObject> GetTestsBySets(int bucketSize, params string[] issueKeys)
        {
            return DoGetByPlanOrSet(bucketSize, TestsBySetFormat, issueKeys);
        }

        public IEnumerable<JObject> GetTestsByPlans(int bucketSize, params string[] issueKeys)
        {
            return DoGetByPlanOrSet(bucketSize, TestsByPlanFormat, issueKeys);
        }

        public IEnumerable<JObject> GetTestCases(int bucketSize, params string[] issueKeys)
        {
            return DoGetTestCases(bucketSize, issueKeys);
        }

        public IEnumerable<JObject> DoGetByPlanOrSet(int bucketSize, string endpointFormar, params string[] issueKeys)
        {
            // setup
            var testSets = jiraClient.GetIssues(issueKeys);

            // exit conditions
            if (!testSets.Any())
            {
                logger?.Warn("Was not able to get test cases from set/plan. Sets/Plans were not found or error occurred.");
                return Array.Empty<JObject>();
            }

            // get requests list
            var data = testSets.Select(i => (Key: $"{i["key"]}", Endpoint: string.Format(endpointFormar, $"{i["id"]}")));

            // get all tests
            var tests = new ConcurrentBag<string>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(data, options, d =>
            {
                var client = GetClientWithToken(d.Key);
                var response = client.GetAsync(d.Endpoint).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }
                var testsArray = JArray.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                var onTests = testsArray.Select(i => $"{i["id"]}");
                tests.AddRange(onTests);
                client.Dispose();
            });

            // get issue keys
            var testCases = jiraClient.GetIssues(tests).Select(i => $"{i["key"]}");

            // get test cases
            return DoGetTestCases(bucketSize, testCases.ToArray());
        }

        private IEnumerable<JObject> DoGetTestCases(int bucketSize, params string[] issueKeys)
        {
            // exit conditions
            if (issueKeys.Length == 0)
            {
                return Array.Empty<JObject>();
            }

            // setup
            var issues = jiraClient.GetIssues(issueKeys);
            var testCases = new ConcurrentBag<JObject>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };

            if (!issues.Any())
            {
                logger?.Warn("No issues of test type found.");
                return Array.Empty<JObject>();
            }

            // queue
            var queue = new ConcurrentQueue<JObject>();
            foreach (var issue in issues)
            {
                queue.Enqueue(issue);
            }

            // client
            var client = GetClientWithToken($"{issues.First()["key"]}");

            // get
            var attempts = 0;
            while (queue.Count > 0 && attempts < queue.Count * 5)
            {
                Parallel.ForEach(queue, options, _ =>
                {
                    queue.TryDequeue(out JObject issueOut);
                    var route = string.Format(StepsFormat, $"{issueOut["id"]}");
                    var response = client.GetAsync(route).GetAwaiter().GetResult();
                    var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    // validate
                    if (!response.IsSuccessStatusCode && responseBody.Contains("Authentication request has expired"))
                    {
                        queue.Enqueue(issueOut);
                        attempts++;
                        return;
                    }
                    else if (!response.IsSuccessStatusCode)
                    {
                        attempts++;
                        return;
                    }

                    // parse
                    var onIssue = JObject.Parse($"{issueOut}");
                    onIssue.Add("steps", JObject.Parse(responseBody).SelectToken("steps"));

                    // results
                    testCases.Add(onIssue);
                });

                // reset token and client
                if (queue.Count > 0)
                {
                    client?.Dispose();
                    client = GetClientWithToken($"{issues.First()["id"]}");
                }
            }

            // cleanup
            client?.Dispose();

            // results
            return testCases;
        }
        #endregion

        #region *** Get Sets          ***
        /// <summary>
        /// Get test sets list based on test case response.
        /// </summary>
        /// <param name="testCase">Test case response body.</param>
        /// <returns>Test sets list (issue ids).</returns>
        public IEnumerable<string> GetSetsByTest(JObject testCase)
        {
            // setup
            var id = $"{testCase["id"]}";
            var key = $"{testCase["key"]}";

            // get client > send request
            var client = GetClientWithToken(key);
            var endpoint = string.Format(SetsFromTestsFormat, id);
            var response = client.GetAsync(endpoint).GetAwaiter().GetResult();

            // validate
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Was unable to get test sets for [{key}].");
                return Array.Empty<string>();
            }

            // extract
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseObjt = JArray.Parse(responseBody);
            if (!responseObjt.Any())
            {
                logger?.Debug($"No tests set for test [{key}].");
                return Array.Empty<string>();
            }
            client.Dispose();
            return responseObjt.Select(i => $"{i.SelectToken("id")}");
        }
        #endregion

        #region *** Get Plans         ***
        /// <summary>
        /// Get test plans list based on test case response.
        /// </summary>
        /// <param name="testCase">Test case response body.</param>
        /// <returns>Test plans list (issue ids).</returns>
        public IEnumerable<string> GetPlansByTest(JObject testCase)
        {
            // setup
            var id = $"{testCase["id"]}";
            var key = $"{testCase["key"]}";

            // get client > send request
            var client = GetClientWithToken(key);
            var endpoint = string.Format(PlansFromTestsFormat, id);
            var response = client.GetAsync(endpoint).GetAwaiter().GetResult();

            // validate
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Was unable to get plans for [{key}].");
                return Array.Empty<string>();
            }

            // extract
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseObjt = JArray.Parse(responseBody);
            if (!responseObjt.Any())
            {
                logger?.Debug($"No test plans for test [{key}].");
                return Array.Empty<string>();
            }
            client.Dispose();
            return responseObjt.Select(i => $"{i.SelectToken("id")}");
        }
        #endregion

        #region *** Get Preconditions ***
        /// <summary>
        /// Get preconditions list based on test case response.
        /// </summary>
        /// <param name="testCase">Test case response body.</param>
        /// <returns>Preconditions list (issue ids).</returns>
        public IEnumerable<string> GetPreconditionsByTest(JObject testCase)
        {
            // setup
            var id = $"{testCase["id"]}";
            var key = $"{testCase["key"]}";

            // get client > send request
            var client = GetClientWithToken(key);
            var endpoint = string.Format(PreconditionsFormat, id);
            var response = client.GetAsync(endpoint).GetAwaiter().GetResult();

            // validate
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Was unable to preconditions for [{key}].");
                return Array.Empty<string>();
            }

            // extract
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseObjt = JArray.Parse(responseBody);
            if (!responseObjt.Any())
            {
                logger?.Debug($"No preconditions for test [{key}].");
                return Array.Empty<string>();
            }
            client.Dispose();
            return responseObjt.Select(i => $"{i.SelectToken("id")}");
        }
        #endregion

        public void AddPrecondition(string precondition, string test)
        {
            // setup
            var onTest = jiraClient.GetIssue(test);
            var onPrecondition = jiraClient.GetIssue(precondition);

            // apply
            var isTest = onTest != default && onTest.ContainsKey("id");
            var isPrecondition = onPrecondition != default && onPrecondition.ContainsKey("id");

            // exit conditions
            if (!isTest || !isPrecondition)
            {
                logger?.Fatal($"Was not able to add precondition [{precondition}] to test [{test}]");
                return;
            }

            // setup request
            var endpoint = string.Format(PreconditionToTestCaseFormat, $"{onTest["id"]}");
            var requestBody = JsonConvert.SerializeObject(new[] { precondition });
            var content = new StringContent(requestBody, Encoding.UTF8, MediaType);

            // setup client > send request
            var client = GetClientWithToken(issueKey: test);
            var response = client.PostAsync(endpoint, content).GetAwaiter().GetResult();

            // log
            if (!response.IsSuccessStatusCode)
            {
                logger?.Fatal($"Was not able to add precondition [{precondition}] to test [{test}]; Error code: {response.StatusCode}");
            }
        }

        public void AddTestsToExecution(int bucketSize, string execution, params string[] tests)
        {
            // setup
            var onExecution = jiraClient.GetIssue(execution);
            var id = $"{onExecution.SelectToken("id")}";
            var key = $"{onExecution.SelectToken("key")}";
            var onTests = jiraClient
                .GetIssues(tests)
                .Select(i => $"{i.SelectToken("id")}")
                .Where(i => i != default)
                .Distinct()
                .Split(200);

            // exit conditions
            if (string.IsNullOrEmpty(id))
            {
                logger?.Error($"Was not able to find execution [{execution}]");
                return;
            }

            // build requests
            var requests = new List<(string Endpoint, HttpContent Content)>();
            var endpoint = string.Format(TestsToExecutionFormat, id);
            foreach (var bulk in onTests)
            {
                var requestBody = JsonConvert.SerializeObject(bulk);
                var content = new StringContent(requestBody, Encoding.UTF8, MediaType);
                requests.Add((endpoint, content));
            }

            // apply
            var client = GetClientWithToken(key);
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(requests, options, request =>
            {
                var response = client.PostAsync(request.Endpoint, request.Content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    logger?.Error($"Was not able to attach test case to [{key}] execution");
                }
            });
            client.Dispose();
        }

        public JObject GetExecutionDetails(string execution, string test)
        {
            // setup
            var issues = jiraClient.GetIssues(new[] { execution, test });

            // exit conditions
            if (!issues.Any())
            {
                logger?.Error($"Was not able to find issues [{execution}, {test}].");
                return default;
            }

            // setup
            var onExecution = issues
                .FirstOrDefault(i => $"{i.SelectToken("key")}".Equals(execution) || $"{i.SelectToken("id")}".Equals(execution));
            var onTest = issues
                .FirstOrDefault(i => $"{i.SelectToken("key")}".Equals(test) || $"{i.SelectToken("id")}".Equals(test));

            // exit conditions
            if(onExecution == default)
            {
                logger?.Error($"Was not able to find execution [{execution}].");
                return default;
            }
            if(onTest == default)
            {
                logger?.Error($"Was not able to find test [{test}].");
                return default;
            }

            // setup            
            var executionKey = $"{onExecution["key"]}";
            var testKey = $"{onTest["key"]}";
            var client = GetClientWithToken(executionKey);
            var token = client.DefaultRequestHeaders.GetValues("X-acpt").FirstOrDefault();
            var requestUri = string.Format(TestExecutionDetailsFormat, testKey, executionKey, token);

            // send
            var response = client.GetAsync(requestUri).GetAwaiter().GetResult();

            // exit  conditions
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Error code: [{response.StatusCode}]; Was not able to get execution details [{test}].");
                return default;
            }

            // parse 
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = Regex.Match(input: responseBody, pattern: "(?<=id=\"test-run\" value=\")[^\"]*").Value.Replace("&quot;", "\"");

            // cleanup
            client.Dispose();

            // results
            return string.IsNullOrEmpty(json) ? default : JObject.Parse(json);
        }

        public void PutStepsRunStatus(string executionKey, string run, params (string Step, string Status)[] steps)
        {
            // setup
            var requests = new List<(string RequestUri, HttpContent Content)>();
            foreach (var (Step, Status) in steps)
            {
                var requestUri = string.Format(TestRunStepStatusFormat, run, Step);
                var content = new StringContent(@"{""status"":""" + Status.ToUpper() + @"""}", Encoding.UTF8, MediaType);
                requests.Add((requestUri, content));
            }

            // send requests
            var client = GetClientWithToken(executionKey);
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            Parallel.ForEach(requests, options, request =>
            {
                var response = client.PostAsync(request.RequestUri, request.Content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    logger?.Error($"Was unable to update test results for [{request.RequestUri}]");
                }
            });
            client.Dispose();
        }

        public void PutTestRunStatus(string executionKey, string run, string status)
        {
            // setup
            var project = $"{jiraClient.GetIssue(executionKey).SelectToken("fields.project.id")}";
            var requestUri = string.Format(TestRunStatusFormat, run);
            var requestObjt = new Dictionary<string, object>
            {
                ["projectId"] = project,
                ["status"] = status.ToUpper()
            };
            var requestBody = JsonConvert.SerializeObject(requestObjt);
            var content = new StringContent(requestBody, Encoding.UTF8, MediaType);

            // send requests
            var client = GetClientWithToken(executionKey);
            var response = client.PostAsync(requestUri, content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error($"Was unable to update test results for [{requestUri}]");
            }
            client.Dispose();
        }

        // UTILITIES
        private string GetToken(string issueKey)
        {
            // constants
            var errorMessage =
                "Was not able to get authentication token for use [" + jiraClient.Authentication.User + "].";

            try
            {
                // get request
                var payload = Assembly
                    .GetExecutingAssembly()
                    .ReadEmbeddedResource("get_token.txt")
                    .Replace("[project-key]", jiraClient.Authentication.Project)
                    .Replace("[issue-key]", issueKey);
                var request = JiraUtilities.GenericPostRequest(Authentication, "/rest/gira/1/", payload);

                // get response
                var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    logger?.Fatal(errorMessage);
                    return string.Empty;
                }

                // parse out authentication token
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var responseObjt = JObject.Parse(responseBody);
                var options = responseObjt.SelectTokens("..options").First().ToString();

                // get token
                return JObject.Parse(options).SelectToken("contextJwt").ToString();
            }
            catch (Exception e) when (e != null)
            {
                logger?.Fatal(errorMessage, e);
                return string.Empty;
            }
        }

        public HttpClient GetClientWithToken(string issueKey)
        {
            // get token
            var token = GetToken(issueKey);

            // new client for each api cycle (since header will change)
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://xray.cloud.xpand-it.com")
            };
            client.DefaultRequestHeaders.Authorization = Authentication.GetAuthenticationHeader();
            client.DefaultRequestHeaders.Add("X-acpt", token);

            // results
            return client;
        }
    }
}