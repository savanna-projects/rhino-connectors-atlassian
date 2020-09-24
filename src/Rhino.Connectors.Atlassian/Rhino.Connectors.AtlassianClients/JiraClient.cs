/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 * https://docs.atlassian.com/software/jira/docs/api/REST/7.13.0/#api/2/issue/{issueIdOrKey}/attachments-addAttachment
 * https://stackoverflow.com/questions/21738782/does-the-jira-rest-api-require-submitting-a-transition-id-when-transitioning-an
 * https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/#api-rest-api-3-issue-issueidorkey-transitions-get
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private readonly ILogger logger;

        // private properties
        private string CreateMessage => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC: Automatically created by Rhino engine.";

        #region *** Constructors   ***
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
            Authentication = authentication;
            this.logger = logger?.CreateChildLogger(loggerName: nameof(JiraClient));

            // project meta data
            ProjectMeta = GetProjectMeta(authentication);
        }
        #endregion

        #region *** Properties     ***
        /// <summary>
        /// Gets the Jira authentication information used by this client.
        /// </summary>
        public JiraAuthentication Authentication { get; }

        /// <summary>
        /// Gets the project meta data used by this client.
        /// </summary>
        public JObject ProjectMeta { get; }
        #endregion

        #region *** Get: Issue     ***
        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="idOrKey">Issue key or id by which to fetch data.</param>
        /// <returns>JSON LINQ object representation of the issue.</returns>
        public JObject GetIssue(string idOrKey)
        {
            return DoGetIssues(bucketSize: 1, idsOrKeys: new[] { idOrKey }).FirstOrDefault();
        }

        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="idsOrKeys">A collection of issue key or by which to fetch data.</param>
        /// <returns>A collection of JSON LINQ object representation of the issue.</returns>
        public IEnumerable<JObject> GetIssues(IEnumerable<string> idsOrKeys)
        {
            // setup
            var bucketSize = Authentication.GetCapability(AtlassianCapabilities.BucketSize, 4);
            logger?.Trace($"Set [{nameof(bucketSize)}] to [{bucketSize}]");

            // get issues
            return DoGetIssues(bucketSize, idsOrKeys);
        }

        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="jql">JQL to search by.</param>
        /// <returns>A collection of JSON LINQ object representation of the issue.</returns>
        public IEnumerable<JObject> GetIssues(string jql)
        {
            return GetIssuesByJql(jql);
        }
        #endregion

        #region *** Post: Issue    ***
        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JObject CreateIssue(string issueBody)
        {
            return DoCraeteOrUpdate(idOrKey: string.Empty, issueBody);
        }

        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JObject CreateIssue(JObject issueBody)
        {
            return DoCraeteOrUpdate(idOrKey: string.Empty, $"{issueBody}");
        }

        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JObject CreateIssue(object issueBody)
        {
            // setup
            var onBody = JsonConvert.SerializeObject(issueBody, JiraUtilities.JsonSettings);

            // post
            return DoCraeteOrUpdate(idOrKey: string.Empty, issueBody: onBody);
        }
        #endregion

        #region *** Put: Issue     ***
        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns><see cref="true"/> if update was successful; <see cref="false"/> if not.</returns>
        public bool UpdateIssue(string idOrKey, string issueBody)
        {
            return DoCraeteOrUpdate(idOrKey, issueBody) != default;
        }

        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns><see cref="true"/> if update was successful; <see cref="false"/> if not.</returns>
        public bool UpdateIssue(string idOrKey, JObject issueBody)
        {
            return DoCraeteOrUpdate(idOrKey, $"{issueBody}") != default;
        }

        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="issueBody">The request body for creating the issue (JSON formatted).</param>
        /// <returns><see cref="true"/> if update was successful; <see cref="false"/> if not.</returns>
        public bool UpdateIssue(string idOrKey, object issueBody)
        {
            // setup
            var onBody = JsonConvert.SerializeObject(issueBody, JiraUtilities.JsonSettings);

            // post
            return DoCraeteOrUpdate(idOrKey, issueBody: onBody) != default;
        }
        #endregion

        #region *** Attachments    ***
        /// <summary>
        /// Add one or more attachments to an issue.
        /// </summary>
        /// <param name="idOrKey">The issue key under which to upload the files.</param>
        /// <param name="files">A collection of files to upload.</param>
        /// <remarks>Default parallel size is 10.</remarks>
        public void AddAttachments(string idOrKey, params string[] files)
        {
            // setup
            var bucketSize = Authentication.GetCapability(AtlassianCapabilities.BucketSize, 10);

            // get requests
            var requests = files.Select(i => JiraUtilities.AddAttachmentRequest(Authentication, idOrKey, path: i));

            // setup
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };

            // add
            Parallel.ForEach(requests, options, DoAddAttachment);
        }

        private void DoAddAttachment(HttpRequestMessage request)
        {
            try
            {
                var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    logger?.Warn(
                        $"Add-Attachment = [{request.RequestUri.AbsoluteUri}]; " +
                        $"Get-Status = [{response.StatusCode}]; " +
                        $"Get-Reason = [{response.ReasonPhrase}]");
                }
            }
            catch (Exception e) when (e != null)
            {
                logger?.Error($"Add-Attachment [{request.RequestUri.AbsoluteUri}] = [False]", e);
            }
        }

        /// <summary>
        /// Remove all attachments from an issue.
        /// </summary>
        /// <param name="idOrKey">The issue key or id by which to remove attachments.</param>
        /// <remarks>Default parallel size is 10.</remarks>
        public void DeleteAttachments(string idOrKey)
        {
            // setup
            var bucketSize = Authentication.GetCapability(AtlassianCapabilities.BucketSize, 10);

            // get issue
            var issue = DoGetIssue(idOrKey, queryString: "?fields=attachment");
            if (issue == default)
            {
                logger?.Warn("Get-Issue = [False]");
                return;
            }

            // check for attachments
            var attachments = issue.SelectToken("fields.attachment");
            if (attachments?.Any() == false)
            {
                logger?.Warn($"Issue {idOrKey} have no attachments.");
                return;
            }

            // build
            var requests = new List<HttpRequestMessage>();
            foreach (var attachment in attachments)
            {
                requests.Add(JiraUtilities.DeleteAttachmentRequest(Authentication, $"{attachment["id"]}"));
            }

            // delete
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(requests, options, request
                => JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult());
        }
        #endregion

        #region *** Get: MetaData  ***
        /// <summary>
        /// Gets a custom field name (e.g. customfield_11) by it's schema.
        /// </summary>
        /// <param name="schema">Schema by which to get custom field.</param>
        /// <returns>Custom field name (e.g. customfield_11).</returns>
        public string GetCustomField(string schema)
        {
            // exit conditions
            if (ProjectMeta == default)
            {
                return default;
            }

            // compose custom field key
            var customFields = ProjectMeta.SelectTokens("..custom").FirstOrDefault(i => $"{i}".Equals(schema, Compare));
            var customField = $"customfield_{customFields.Parent.Parent["customId"]}";

            // logging
            logger?.Debug($"Get-CustomField [{schema}] = [{customField}]");
            return customField;
        }

        /// <summary>
        /// Gets the literal issue type as returned by Jira server.
        /// </summary>
        /// <param name="idOrKey">The issue key or id by which to get type.</param>
        /// <returns>The issue type as returned by Jira server.</returns>
        public string GetIssueType(string idOrKey)
        {
            // get issue & validate response
            var issue = DoGetIssues(bucketSize: 1, new[] { idOrKey }).FirstOrDefault();

            // extract issue type
            return ExtractIssueType(issue);
        }

        /// <summary>
        /// Gets the literal issue type as returned by Jira server.
        /// </summary>
        /// <param name="issue">Issue token by which to fetch data.</param>
        /// <returns>The issue type as returned by Jira server.</returns>
        public string GetIssueType(JObject issue)
        {
            return ExtractIssueType(issue);
        }

        /// <summary>
        /// Gets available field and values of an issue type.
        /// </summary>
        /// <param name="idOrKey">The issue type for which to get fields and values.</param>
        /// <param name="path">A <see cref="string"/> that contains a JSONPath expression.</param>
        /// <returns>Serialized field token.</returns>
        public string GetIssueTypeFields(string idOrKey, string path)
        {
            // exit conditions
            if (ProjectMeta == default)
            {
                return string.Empty;
            }

            // issue type
            var issueTypeToken = ProjectMeta["issuetypes"].FirstOrDefault(i => $"{i["name"]}".Equals(idOrKey, Compare));

            // exit conditions
            if (issueTypeToken == default)
            {
                return string.Empty;
            }
            else if (string.IsNullOrEmpty(path))
            {
                return $"{issueTypeToken}";
            }

            // field token
            var issueFieldToken = issueTypeToken.SelectTokens(path).First();

            // exit conditions
            return issueFieldToken == null ? string.Empty : $"{issueFieldToken}";
        }

        /// <summary>
        /// Gets available transitions by issue.
        /// </summary>
        /// <param name="idOrKey">The key of the issue by which to get transitions.</param>
        /// <returns>A collection of transitions.</returns>
        public IEnumerable<IDictionary<string, string>> GetTransitions(string idOrKey)
        {
            // get & verify response
            var request = JiraUtilities.GetTransitionsRequest(Authentication, idOrKey);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Get-Request = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return Array.Empty<IDictionary<string, string>>();
            }

            // extract data
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!responseBody.IsJson())
            {
                return Array.Empty<IDictionary<string, string>>();
            }
            var transitionsCollection = JObject.Parse(responseBody)["transitions"];

            // build
            var transitions = new List<IDictionary<string, string>>();
            foreach (var transition in transitionsCollection)
            {
                var entry = new Dictionary<string, string>
                {
                    ["id"] = $"{transition["id"]}",
                    ["name"] = $"{transition["name"]}",
                    ["to"] = transition.SelectToken("to.name") == null ? "N/A" : $"{transition.SelectToken("to.name")}"
                };
                transitions.Add(entry);
            }

            // results
            return transitions;
        }
        #endregion

        #region *** Post: MetaData ***
        /// <summary>
        /// Creates an issue link between 2 issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key if the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key if the outward issue (i.e. the issue which is blocked by).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public void CreateIssueLink(string linkType, string inward, string outward)
        {
            DoCreateIssueLink(linkType, inward, outward, comment: CreateMessage);
        }

        /// <summary>
        /// Creates an issue link between 2 issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key of the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key of the outward issue (i.e. the issue which is blocked by).</param>
        /// <param name="comment">Comment to create for this link.</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public void CreateIssueLink(string linkType, string inward, string outward, string comment)
        {
            DoCreateIssueLink(linkType, inward, outward, comment);
        }

        private void DoCreateIssueLink(string linkType, string inward, string outward, string comment)
        {
            // content & request
            var request = JiraUtilities.CreateLinkRequest(Authentication, linkType, inward, outward, comment);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();

            // logging
            if (response.IsSuccessStatusCode)
            {
                logger?.Debug($"Create-IssueLink [{linkType}] [{inward}] [{outward}] = [True]");
            }
            else
            {
                var exception = new HttpRequestException(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                var message = $"Create-IssueLink [{linkType}] [{inward}] [{outward}] = [False]";
                logger?.Error(message, exception);
            }
        }

        /// <summary>
        /// Performs an issue transition and, if the transition has a screen, updates the fields from the transition screen.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transitionId">The ID of the transition (you can use GetTransitions method to get the transition ID).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        public bool CreateTransition(string idOrKey, string transitionId, string resolution)
        {
            return DoCreateTransition(idOrKey, transitionId, resolution, comment: CreateMessage);
        }

        /// <summary>
        /// Performs an issue transition and, if the transition has a screen, updates the fields from the transition screen.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transitionId">The ID of the transition (you can use GetTransitions method to get the transition ID).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        /// <param name="comment">A comment to add when posting transition</param>
        public bool CreateTransition(string idOrKey, string transitionId, string resolution, string comment)
        {
            return DoCreateTransition(idOrKey, transitionId, resolution, comment);
        }

        private bool DoCreateTransition(string idOrKey, string transitionId, string resolution, string comment)
        {
            // content & request
            var request = JiraUtilities.CreateTransitionRequest(Authentication, idOrKey, transitionId, resolution, comment);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();

            // logging
            if (response.IsSuccessStatusCode)
            {
                logger?.Debug($"Create-Transition [{idOrKey}] [{transitionId}] [{resolution}] = [True]");
            }
            else
            {
                var exception = new HttpRequestException(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                var message = $"Create-Transition [{idOrKey}] [{transitionId}] [{resolution}] = [False]";
                logger?.Error(message, exception);
            }

            // get
            return response.IsSuccessStatusCode;
        }
        #endregion

        #region *** Post: Comments ***
        /// <summary>
        /// Add comment to issue
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="comment">Comment to apply.</param>
        /// <returns><see cref="true"/> if successful; <see cref="false"/> if not.</returns>
        public bool CreateComment(string idOrKey, string comment)
        {
            // get request body
            var request = JiraUtilities.CreateCommentRequest(Authentication, idOrKey, comment);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Create-Request = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return false;
            }

            // get
            return response.IsSuccessStatusCode;
        }
        #endregion

        #region *** Utilities      ***
        // gets a collection of issues using intervals and bucket size for maximum performance
        private IEnumerable<JObject> DoGetIssues(int bucketSize, IEnumerable<string> idsOrKeys)
        {
            // split in buckets
            var buckets = idsOrKeys.Split(10);
            logger?.Trace($"Set-Parameter [{nameof(buckets)}] = [{buckets.Count()}]");

            // build queries (groups of 10)
            var jqls = new List<string>();
            foreach (var bucket in buckets)
            {
                jqls.Add($"key in ({string.Join(",", bucket)})");
            }
            logger?.Trace($"Set-Parameter [{nameof(jqls)}] = [{jqls.Count}]");

            // setup
            var objectCollection = new ConcurrentBag<JObject>();

            // collect
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            logger?.Trace($"Set-Parameter [{nameof(parallelOptions)}] = [{bucketSize}]");

            Parallel.ForEach(jqls, parallelOptions, jql =>
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
            // get & verify response
            var request = JiraUtilities.GetByJqlRequest(Authentication, jql);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Get-Request = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return default;
            }

            // parse into JObject
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // validate
            if (!responseBody.IsJson())
            {
                logger?.Warn("Get-IssueAsJson = [False]");
                return Array.Empty<JObject>();
            }

            // deserialize
            var obj = JObject.Parse(responseBody);

            // validate
            if (!obj.ContainsKey("issues") || !obj["issues"].Any())
            {
                logger?.Warn("Get-IssueFromBody = [False]");
                return Array.Empty<JObject>();
            }

            // parse and return
            return obj["issues"].Select(i => JObject.Parse($"{i}"));
        }

        // gets an issue
        private JObject DoGetIssue(string idOrKey, string queryString)
        {
            // get & verify response
            var request = JiraUtilities.GetRequest(Authentication, idOrKey, queryString);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Get-Request = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return default;
            }

            // results
            return response.ToObject();
        }

        // creates or updates an issue by id or key
        private JObject DoCraeteOrUpdate(string idOrKey, string issueBody)
        {
            // get request body
            var request = JiraUtilities.CreateOrUpdateRequst(Authentication, idOrKey, issueBody);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Create-Request = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return default;
            }

            // parse body
            var responseBody = response.ToObject();

            // update test run key
            var key = responseBody["key"];
            logger?.Debug($"Create-Issue [{key}] = [True]");

            // results
            return responseBody;
        }

        // extract issue type from issue JSON response
        private string ExtractIssueType(JObject issue)
        {
            return issue == default ? "-1" : $"{issue.SelectToken("fields.issuetype.name")}";
        }

        // extract project meta data object
        private JObject GetProjectMeta(JiraAuthentication authentication)
        {
            // get & verify response
            var request = JiraUtilities.GetMetaRequest(authentication);
            var response = JiraUtilities.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                logger?.Warn(
                    $"Get-Meta = [{request.RequestUri.AbsoluteUri}]; " +
                    $"Get-Status = [{response.StatusCode}]; " +
                    $"Get-Reason = [{response.ReasonPhrase}]");
                return default;
            }

            // parse into JObject
            return response.ToObject();
        }
        #endregion
    }
}