/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 * https://docs.atlassian.com/software/jira/docs/api/REST/7.13.0/#api/2/issue/{issueIdOrKey}/attachments-addAttachment
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
        private readonly string metaFormat;
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Gets the <see cref="System.Net.Http.HttpClient"/> used by this JiraClient.
        /// </summary>
        public static readonly HttpClient HttpClient = new HttpClient();

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
            metaFormat = $"/rest/api/{apiVersion}/issue/createmeta?projectKeys={authentication.Project}&expand=projects.issuetypes.fields";

            // setup: provider authentication and base address
            var header = $"{authentication.User}:{authentication.Password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

            // public client
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedHeader);
            HttpClient.BaseAddress = new Uri(authentication.Collection);

            // private client
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedHeader);
            httpClient.DefaultRequestHeaders.AddIfNotExists("X-Atlassian-Token", "no-check");
        }
        #endregion

        #region *** GET          ***
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

        /// <summary>
        /// Gets available field and values of an issue type.
        /// </summary>
        /// <param name="issueType">The issue type for which to get fields and values.</param>
        /// <returns>Serialized field token.</returns>
        public string GetIssueTypeFields(string issueType, string field)
        {
            // get meta
            var response = HttpClient.GetAsync(metaFormat).GetAwaiter().GetResult();

            // exit conditions
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            // parse
            var metaBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var metaObjt = JObject.Parse(metaBody)["projects"].FirstOrDefault();

            // exit conditions
            if (metaBody == default)
            {
                return string.Empty;
            }

            // issue type
            var issueTypeToken = metaObjt["issuetypes"].FirstOrDefault(i => $"{i["name"]}".Equals(issueType, Compare));

            // exit conditions
            if (issueTypeToken == default)
            {
                return string.Empty;
            }
            else if (string.IsNullOrEmpty(field))
            {
                return $"{issueTypeToken}";
            }

            // field token
            var issueFieldToken = issueTypeToken.SelectToken($"fields.{field}");

            // exit conditions
            return issueFieldToken == null ? string.Empty : $"{issueFieldToken}";
        }
        #endregion

        #region *** POST         ***
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
            var content = new StringContent(requestBody, Encoding.UTF8, JiraClient.MediaType);
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

        /// <summary>
        /// Creates an issue link between 2 issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key if the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key if the outward issue (i.e. the issue which is blocked by).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public void CreateIssueLink(string linkType, string inward, string outward)
        {
            // setup
            var requestObjt = new
            {
                Type = new
                {
                    Name = linkType
                },
                InwardIssue = new
                {
                    Key = inward
                },
                OutwardIssue = new
                {
                    Key = outward
                },
                Comment = new
                {
                    Body = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC: Automatically created by Rhino engine."
                }
            };
            var requestBody = JsonConvert.SerializeObject(requestObjt, JsonSettings);

            // content & request
            var content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);
            var response = HttpClient.PostAsync("/rest/api/latest/issueLink", content).GetAwaiter().GetResult();

            // logging
            if (response.IsSuccessStatusCode)
            {
                logger?.Debug($"Link of type [{linkType}] was created between [{inward}] and [{outward}]");
            }
            else
            {
                var exception = new HttpRequestException(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                var message =
                    $"Was not able to create link of type [{linkType}] between [{inward}] and [{outward}]; Status code [{response.StatusCode}]";
                logger?.Error(message, exception);
            }
        }

        /// <summary>
        /// Add one or more attachments to an issue.
        /// </summary>
        /// <param name="issueKey">The issue key under which to upload the files.</param>
        /// <param name="files">A collection of files to upload.</param>
        /// <remarks>Default parallel size is 10.</remarks>
        public void AddAttachments(string issueKey, params string[] files)
        {
            DoAddAttachments(bucketSize: 10, issueKey, files);
        }

        /// <summary>
        /// Add one or more attachments to an issue.
        /// </summary>
        /// <param name="bucketSize">How many files will be uploaded in parallel.</param>
        /// <param name="issueKey">The issue key under which to upload the files.</param>
        /// <param name="files">A collection of files to upload.</param>
        public void AddAttachments(int bucketSize, string issueKey, params string[] files)
        {
            DoAddAttachments(bucketSize, issueKey, files);
        }

        private void DoAddAttachments(int bucketSize, string issueKey, params string[] files)
        {
            // get requests
            var attachmentRequests = files.Select(i => GetAttachmentRequest(issueKey, path: i));

            // setup
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(attachmentRequests, options, request =>
            {
                try
                {
                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        logger?.Warn(
                            $"Failed to upload attachment to [{issueKey}]; " +
                            $"Reason [{response.StatusCode}]:[{response.ReasonPhrase}]");
                    }
                }
                catch (Exception e) when (e != null)
                {
                    logger?.Error($"Failed to upload attachment to [{issueKey}]", e);
                }
            });
        }

        private HttpRequestMessage GetAttachmentRequest(string issueKey, string path)
        {
            // base address
            var baseAddress = HttpClient.BaseAddress.AbsoluteUri.EndsWith("/")
                ? HttpClient.BaseAddress.AbsoluteUri.Substring(0, HttpClient.BaseAddress.AbsoluteUri.LastIndexOf('/'))
                : HttpClient.BaseAddress.AbsoluteUri;

            // setup
            var bytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            var endpoint = new Uri(string.Format("{0}/{1}/attachments", $"{baseAddress}{issueFormat}", issueKey));

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);

            // setup: request data
            var boundary = Guid.NewGuid();
            var multipartContent = new MultipartFormDataContent($"----{boundary}");

            var byteContent = new ByteArrayContent(bytes);
            byteContent.Headers.Add("X-Atlassian-Token", "no-check");

            multipartContent.Add(byteContent, "file", fileName);

            // apply to request message
            requestMessage.Content = multipartContent;

            // results
            return requestMessage;
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