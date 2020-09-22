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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Rhino.Connectors.Xray.Cloud
{
    internal class XpandClient
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;
        private const string StepsFormat = "/api/internal/test/{0}/steps?startAt=0&maxResults=100";

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

        #region *** Constructors ***
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

        #region *** Properties   ***
        /// <summary>
        /// Jira authentication information.
        /// </summary>
        public JiraAuthentication Authentication { get; }
        #endregion

        public JObject GetTestCase(string issueKey)
        {
            return DoGetTestCases(bucketSize: 1, issueKey).FirstOrDefault();
        }

        public IEnumerable<JObject> GetTestCases(int bucketSize, params string[] issueKeys)
        {
            return DoGetTestCases(bucketSize, issueKeys);
        }

        private IEnumerable<JObject> DoGetTestCases(int bucketSize, params string[] issueKeys)
        {
            // exit conditions
            if (issueKeys.Length == 0)
            {
                return Array.Empty<JObject>();
            }

            // setup
            var issues = jiraClient.GetIssues(bucketSize, issueKeys);
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

        // UTILITIES
        private string GetToken(string issueKey)
        {
            // constants
            var errorMessage =
                "Was not able to get authentication token for use [" + jiraClient.Authentication.User + "].";

            try
            {
                // get request
                var requestBody = Assembly
                    .GetExecutingAssembly()
                    .ReadEmbeddedResource("get_token.txt")
                    .Replace("[project-key]", jiraClient.Authentication.Project)
                    .Replace("[issue-key]", issueKey);

                // setup: request content
                var content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);

                // get response
                var response = JiraClient.HttpClient.PostAsync("/rest/gira/1/", content).GetAwaiter().GetResult();
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

        private HttpClient GetClientWithToken(string issueKey)
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