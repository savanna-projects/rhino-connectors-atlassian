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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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
            jqlFormat = string.Format("/rest/api/{0}/search?jql=id", apiVersion);
            issueFormat = $"/rest/api/{apiVersion}/issue";

            // setup: provider authentication and base address
            var header = $"{"admin"}:{"admin"}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedHeader);
            HttpClient.BaseAddress = new Uri(authentication.Collection);
        }
        #endregion

        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="issueKey">Issue key by which to fetch data.</param>
        /// <returns>JSON LINQ object representation of the issue.</returns>
        public JObject GetIssue(string issueKey)
        {
            return DoGetIssue(issueKey);
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
            var jsonObject = DoGetIssue(issueKey);

            // extract issue type
            return jsonObject == default ? "-1" : $"{jsonObject.SelectToken("fields.issuetype.name")}";
        }

        // UTILITIES
        private JObject DoGetIssue(string issueKey)
        {
            // constants: logging
            const string E = "Fetching issue [{0}] returned with status code [{1}] and message [{2}]";
            const string M = "Issue [{0}] fetched and converted into json object";
            const string W = "Issue [{0}] was not found";

            // compose endpoint
            var endpoint = $"{jqlFormat}={issueKey}";

            // get & verify response
            var httpResponseMessage = HttpClient.GetAsync(endpoint).GetAwaiter().GetResult();
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                logger?.WarnFormat(E, issueKey, httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase);
                return default;
            }

            // parse into JObject
            var issueToken = httpResponseMessage.ToObject()["issues"];
            logger?.DebugFormat(M, issueKey);

            // validate
            if (!issueToken.Any())
            {
                logger?.WarnFormat(W, issueKey);
                return default;
            }
            return JObject.Parse($"{issueToken[0]}");
        }
    }
}