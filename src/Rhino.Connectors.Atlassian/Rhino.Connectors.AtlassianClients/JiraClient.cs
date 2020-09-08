/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Rhino.Connectors.AtlassianClients
{
    /// <summary>
    /// Jira client for Rhino's products line.
    /// </summary>
    public class JiraClient
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        // members
        private readonly JiraAuthentication authentication;
        private readonly ILogger logger;
        private readonly string jqlFormat;
        private readonly string issueFormat;

        /// <summary>
        /// Gets the <see cref="System.Net.Http.HttpClient"/> used by this JiraClient.
        /// </summary>
        public static readonly HttpClient HttpClient = new HttpClient();

        #region *** Constructors ***
        /// <summary>
        /// Creates a new instance of JiraClient.
        /// </summary>
        /// <param name="authentication">Authentication information by which to connect and fetch data from Jira.</param>
        public JiraClient(JiraAuthentication authentication)
            : this(authentication, logger: default)
        { }

        /// <summary>
        /// Creates a new instance of JiraClient.
        /// </summary>
        /// <param name="authentication">Authentication information by which to connect and fetch data from Jira.</param>
        /// <param name="logger">Logger implementation for this client.</param>
        public JiraClient(JiraAuthentication authentication, ILogger logger)
        {
            // setup
            this.authentication = authentication;
            this.logger = logger?.CreateChildLogger(loggerName: nameof(JiraClient));

            // url format
            var apiVersion = authentication.Capabilities.ContainsKey("apiVersion")
                ? $"{authentication.Capabilities["apiVersion"]}"
                : "latest";
            jqlFormat = string.Format("/rest/api/{0}/search?jql=", apiVersion);
            issueFormat = $"/rest/api/{apiVersion}/issue";

            // setup: provider authentication and base address
            var header = $"{authentication.User}:{authentication.Password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedHeader);
            HttpClient.BaseAddress = new Uri(authentication.Collection);
        }
        #endregion

        #region *** GET  ***
        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="issueKey">Issue key by which to fetch data.</param>
        /// <returns>JSON LINQ object representation of the issue.</returns>
        public JObject GetIssue(string issueKey)
        {
            return DoGetIssues(bucketSize: 1, issueKey).FirstOrDefault();
        }

        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="bucketSize">The number of parallel request to Jira. Each request will fetch 10 issues.</param>
        /// <param name="issuesKeys">Issue key by which to fetch data.</param>
        /// <returns>A collection of JSON LINQ object representation of the issue.</returns>
        public IEnumerable<JObject> GetIssues(int bucketSize, params string[] issuesKeys)
        {
            // setup
            bucketSize = bucketSize == 0 ? 4 : bucketSize;

            // get issues
            return DoGetIssues(bucketSize, issuesKeys);
        }

        /// <summary>
        /// Gets a custom field name (e.g. customfield_11) by it's schema.
        /// </summary>
        /// <param name="schema">Schema by which to get custom field.</param>
        /// <returns>Custom field name (e.g. customfield_11).</returns>
        public string GetCustomField(string schema)
        {
            // constants: logging
            const string W = "Fetching custom field for schema [{0}] returned with status code [{1}] and message [{2}]";
            const string M = "Custom field [{0}] found for schema [{1}]";

            // compose endpoint
            var routing = $"{issueFormat}/createmeta?projectKeys={authentication.Project}&expand=projects.issuetypes.fields";

            // get & verify response
            var httpResponseMessage = HttpClient.GetAsync(routing).GetAwaiter().GetResult();
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                logger?.WarnFormat(W, schema, httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase);
                return "-1";
            }

            // parse into JObject
            var jsonObject = httpResponseMessage.ToObject();

            // compose custom field key
            var customFields = jsonObject.SelectTokens("..custom").FirstOrDefault(i => $"{i}".Equals(schema, Compare));
            var customField = $"customfield_{customFields.Parent.Parent["customId"]}";

            // logging
            logger?.DebugFormat(M, customField, schema);
            return customField;
        }

        /// <summary>
        /// Gets the literal issue type as returned by Jira server.
        /// </summary>
        /// <param name="issueKey">Issue key by which to fetch data.</param>
        /// <returns>The issue type as returned by Jira server.</returns>
        public string GetIssueType(string issueKey)
        {
            // get issue & validate response
            var jsonObject = DoGetIssues(bucketSize: 1, issueKey).FirstOrDefault();

            // extract issue type
            return DoGetIssueType(jsonObject);
        }

        /// <summary>
        /// Gets the literal issue type as returned by Jira server.
        /// </summary>
        /// <param name="issueToken">Issue token by which to fetch data.</param>
        /// <returns>The issue type as returned by Jira server.</returns>
        public string GetIssueType(JObject issueToken)
        {
            return DoGetIssueType(issueToken);
        }
        #endregion

        #region *** POST ***
        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="issueBody">The request body for creating the issue.</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JObject CreateIssue(string issueBody)
        {
            return DoCraeteIssue($"{issueBody}");
        }

        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="issueToken">The request body for creating the issue.</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JObject CreateIssue(JObject issueToken)
        {
            return DoCraeteIssue($"{issueToken}");
        }

        private JObject DoCraeteIssue(string requestBody)
        {
            // constants: logging
            const string W = "Failed to the issue; response code [{0}]; reason phrase [{1}]";
            const string M = "Issue [{0}] created.";

            // get request body
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = HttpClient.PostAsync(issueFormat, content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                logger?.WarnFormat(W, response.StatusCode, response.ReasonPhrase);
                return default;
            }

            // parse body
            var responseBody = response.ToObject();

            // update test run key
            var key = responseBody["key"].ToString();
            logger?.DebugFormat(M, key);

            // results
            return responseBody;
        }
        #endregion

        // UTILITIES
        private string DoGetIssueType(JObject issueToken)
        {
            return issueToken == default ? "-1" : $"{issueToken.SelectToken("fields.issuetype.name")}";
        }

        private IEnumerable<JObject> DoGetIssues(int bucketSize, params string[] issuesKeys)
        {
            // split in buckets
            var buckets = issuesKeys.Split(10);

            // build queries (groups of 10)
            var jqls = new List<string>();
            foreach (var bucket in buckets)
            {
                jqls.Add($"{jqlFormat} key in ({string.Join(",", bucket)})");
            }

            // setup
            var objectCollection = new ConcurrentBag<JObject>();

            // collect
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(jqls, options, jql =>
            {
                foreach (var item in GetIssuesByJql(jql))
                {
                    objectCollection.Add(item);
                }
            });
            return objectCollection;
        }

        private IEnumerable<JObject> GetIssuesByJql(string jql)
        {
            // constants: logging
            const string E = "Fetching issues [{0}] returned with status code [{1}] and message [{2}]";
            const string M = "Issues [{0}] fetched and converted into json object";
            const string W = "Issues [{0}] was not found";

            // get & verify response
            var response = HttpClient.GetAsync(jql).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.WarnFormat(E, jql, response.StatusCode, response.ReasonPhrase);
                return default;
            }

            // parse into JObject
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            logger?.DebugFormat(M, jql);

            // validate
            if (!responseBody.IsJson())
            {
                logger?.WarnFormat(W, jql);
                return Array.Empty<JObject>();
            }

            // deserialize
            var obj = JObject.Parse(responseBody);

            // validate
            if (!obj.ContainsKey("issues") || !obj["issues"].Any())
            {
                logger?.WarnFormat(W, jql);
                return Array.Empty<JObject>();
            }

            // parse and return
            return obj["issues"].Select(i => JObject.Parse($"{i}"));
        }
    }
}