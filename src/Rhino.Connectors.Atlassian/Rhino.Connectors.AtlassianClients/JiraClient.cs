/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 * https://developer.atlassian.com/cloud/jira/platform/rest/v3/intro/
 * https://docs.atlassian.com/software/jira/docs/api/REST/7.13.0/#api/2/issue/{issueIdOrKey}/attachments-addAttachment
 * https://stackoverflow.com/questions/21738782/does-the-jira-rest-api-require-submitting-a-transition-id-when-transitioning-an
 * https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/#api-rest-api-3-issue-issueidorkey-transitions-get
 */
using Gravity.Abstraction.Logging;
using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        private readonly JiraCommandsExecutor executor;

        // private properties
        private static string CreateMessage
            => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC: Automatically created by Rhino engine.";

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
            this.logger = logger?.CreateChildLogger(loggerName: nameof(JiraClient));
            Authentication = authentication;
            executor = new JiraCommandsExecutor(authentication, logger);

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
        public JToken ProjectMeta { get; }
        #endregion

        #region *** Get: Issue     ***
        /// <summary>
        /// Gets the details for an issue.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue.</param>
        /// <returns>JSON LINQ object representation of the issue.</returns>
        public JToken Get(string idOrKey, params string[] fields)
        {
            return JiraCommandsRepository.Get(idOrKey, fields).Send(executor);
        }

        /// <summary>
        /// Gets the details for a collection of issues.
        /// </summary>
        /// <param name="idsOrKeys">A collection ID or key of the issue.</param>
        /// <returns>A collection of JSON LINQ object representation of the issue.</returns>
        public IEnumerable<JToken> Get(IEnumerable<string> idsOrKeys)
        {
            // setup
            var bucketSize = Authentication.GetCapability(AtlassianCapabilities.BucketSize, 4);
            logger?.Trace($"Set-Parameter [{nameof(bucketSize)}] = [{bucketSize}]");

            // get issues
            return DoSearch(bucketSize, idsOrKeys);
        }

        /// <summary>
        /// Gets a Jira issue a JSON LINQ object.
        /// </summary>
        /// <param name="jql">JQL to search by.</param>
        /// <returns>A collection of JSON LINQ object representation of the issue.</returns>
        public IEnumerable<JToken> Search(string jql)
        {
            return DoSearch(jql);
        }

        /// <summary>
        /// Gets a JWT (token) from Jira using current credentials.
        /// </summary>
        /// <param name="key">The issue key.</param>
        /// <returns>A JWT</returns>
        public string GetJwt(string key)
        {
            // get
            var response = JiraCommandsRepository.GetToken(Authentication.Project, key).Send(executor).AsJToken();

            // extract
            var options = response.SelectTokens("..options").First().ToString();

            // get
            return JToken.Parse(options).SelectToken("contextJwt").ToString();
        }

        /// <summary>
        /// Gets the user information.
        /// </summary>
        /// <param name="key">The issue key.</param>
        /// <param name="nameOrAddress">The user (assignee) email.</param>
        /// <returns>The assignee account ID.</returns>
        public JToken GetUser(string key, string nameOrAddress)
        {
            return DoGetUser(key, nameOrAddress);
        }

        /// <summary>
        /// Assign to the current authenticated user.
        /// </summary>
        /// <param name="key">The issue key.</param>
        public void Assign(string key)
        {
            DoAssign(key, Authentication.User);
        }

        /// <summary>
        /// Assign to the provided user.
        /// </summary>
        /// <param name="key">The issue key.</param>
        public void Assign(string key, string nameOrAddress)
        {
            DoAssign(key, nameOrAddress);
        }

        private void DoAssign(string key, string nameOrAddress)
        {
            // get
            var onUser = DoGetUser(key, nameOrAddress);

            // setup
            var id = $"{onUser.SelectToken("accountId")}";

            // assign
            JiraCommandsRepository.Assign(idOrKey: key, $"{id}").Send(executor);
        }

        private JToken DoGetUser(string key, string nameOrAddress)
        {
            // get
            var users = JiraCommandsRepository
                .GetAssignableUsers(key)
                .Send(executor)
                .AsJToken()
                .Select(i => i.AsJObject());

            // parse displayName
            var user = users
                .FirstOrDefault(i => $"{i.SelectToken("emailAddress")}".Equals(nameOrAddress, Compare)
                    || $"{i.SelectToken("displayName")}".Equals(nameOrAddress, Compare));

            // get
            return user == default ? JObject.Parse("{}") : user;
        }
        #endregion

        #region *** Post: Issue    ***
        /// <summary>
        /// Creates an issue.
        /// </summary>
        /// <param name="data">The request body for creating the issue (JSON formatted).</param>
        /// <returns>Response as JSON LINQ Object instance.</returns>
        public JToken Create(object data)
        {
            return DoCraeteOrUpdate(idOrKey: string.Empty, data: data);
        }
        #endregion

        #region *** Put: Issue     ***
        /// <summary>
        /// Creates an issue under Jira Server.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="data">The request body for creating the issue (JSON formatted).</param>
        /// <returns><see cref="true"/> if update was successful; <see cref="false"/> if not.</returns>
        public bool UpdateIssue(string idOrKey, object data)
        {
            return DoCraeteOrUpdate(idOrKey, data: data) != default;
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
            var onFiles = files.Select(i => (i, "image/png")).ToArray();

            // add
            executor.AddAttachments(idOrKey, onFiles);
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
            var issue = JiraCommandsRepository.Get(idOrKey, fields: "attachment").Send(executor).AsJToken();
            if ($"{issue["id"]}" == "-1")
            {
                logger?.Warn("Get-Issue = false");
                return;
            }

            // build
            var commands = issue
                .SelectToken("fields.attachment")
                .Where(i => i != default)
                .Select(i => JiraCommandsRepository.DeleteAttachment($"{i.SelectToken("id")}"));

            // delete
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            Parallel.ForEach(commands, options, command => command.Send(executor));
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
            var customFields = ProjectMeta.AsJObject().SelectTokens("..custom").FirstOrDefault(i => $"{i}".Equals(schema, Compare));
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
            var issue = DoSearch(bucketSize: 1, new[] { idOrKey }).FirstOrDefault();

            // extract issue type
            return ExtractIssueType(issue);
        }

        /// <summary>
        /// Gets the literal issue type as returned by Jira server.
        /// </summary>
        /// <param name="issue">Issue token by which to fetch data.</param>
        /// <returns>The issue type as returned by Jira server.</returns>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Designed to be an instance public method.")]
        public string GetIssueType(JToken issue)
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
            return DoGetTransitions(idOrKey);
        }
        #endregion

        #region *** Post: MetaData ***
        /// <summary>
        /// Creates an issue link between 2 issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key if the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key if the outward issue (i.e. the issue which is blocked by).</param>
        public void CreateIssueLink(string linkType, string inward, string outward)
        {
            DoCreateIssueLink(linkType, inward, outward, comment: CreateMessage);
        }

        /// <summary>
        /// Creates an link between two issues.
        /// </summary>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key of the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key of the outward issue (i.e. the issue which is blocked by).</param>
        /// <param name="comment">Comment to create for this link.</param>
        public void CreateIssueLink(string linkType, string inward, string outward, string comment)
        {
            DoCreateIssueLink(linkType, inward, outward, comment);
        }

        private void DoCreateIssueLink(string linkType, string inward, string outward, string comment)
        {
            JiraCommandsRepository.CreateIssueLink(linkType, inward, outward, comment).Send(executor);
        }

        /// <summary>
        /// Performs an issue transition and, if the transition has a screen, updates the fields from the transition screen.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transition">The name of the transition (you can use GetTransitions method to get the transition names).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> if not.</returns>
        public bool CreateTransition(string idOrKey, string transition, string resolution)
        {
            return DoCreateTransition(idOrKey, transition, resolution, comment: CreateMessage);
        }

        /// <summary>
        /// Performs an issue transition and, if the transition has a screen, updates the fields from the transition screen.
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transition">The name of the transition (you can use GetTransitions method to get the transition names).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        /// <param name="comment">A comment to add when posting transition</param>
        /// <returns><see cref="true"/> if successful, <see cref="false"/> if not.</returns>
        public bool CreateTransition(string idOrKey, string transition, string resolution, string comment)
        {
            return DoCreateTransition(idOrKey, transition, resolution, comment);
        }

        private bool DoCreateTransition(string idOrKey, string transition, string resolution, string comment)
        {
            // setup
            var transitions = DoGetTransitions(idOrKey);

            // exit conditions
            if (!transitions.Any())
            {
                return false;
            }

            // get transition
            var onTransition = transitions.FirstOrDefault(i => i["to"].Equals(transition, Compare));
            if (onTransition == default)
            {
                logger?.Info($"Get-Transition -Key [{idOrKey}] -Transition [{transition}] = false");
                return false;
            }

            //send transition
            var response = JiraCommandsRepository
                .CreateTransition(idOrKey, onTransition["id"], resolution, comment)
                .Send(executor)
                .AsJToken();

            // get
            return $"{response.SelectToken("code")}" == "204";
        }
        #endregion

        #region *** Post: Comments ***
        /// <summary>
        /// Add comment to issue
        /// </summary>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="comment">Comment to apply.</param>
        /// <returns><see cref="true"/> if successful; <see cref="false"/> if not.</returns>
        public bool AddComment(string idOrKey, string comment)
        {
            // post
            var respose = JiraCommandsRepository.AddComment(idOrKey, comment).Send(executor).AsJToken();

            // assert
            return $"{respose.SelectToken("code")}" == "204";
        }
        #endregion

        #region *** Utilities      ***
        // gets a collection of issues using intervals and bucket size for maximum performance
        private IEnumerable<JToken> DoSearch(int bucketSize, IEnumerable<string> idsOrKeys)
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
            var objectCollection = new ConcurrentBag<JToken>();

            // collect
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };
            logger?.Trace($"Set-Parameter [{nameof(parallelOptions)}] = [{bucketSize}]");

            Parallel.ForEach(jqls, parallelOptions, jql =>
            {
                foreach (var item in DoSearch(jql))
                {
                    objectCollection.Add(item);
                }
            });
            return objectCollection;
        }

        // TODO: implement fetching strategy for large numbers
        private IEnumerable<JToken> DoSearch(string jql)
        {
            // parse
            var issues = JiraCommandsRepository.Search(jql).Send(executor).AsJToken().SelectToken("issues");

            // get
            return issues == default || !(issues is JArray) ? JToken.Parse("[]") : issues;
        }

        // creates or updates an issue by id or key
        private JToken DoCraeteOrUpdate(string idOrKey, object data)
        {
            // setup conditions
            var isUpdate = !string.IsNullOrEmpty(idOrKey);

            // setup
            var command = isUpdate
                ? JiraCommandsRepository.Update(idOrKey, data)
                : JiraCommandsRepository.Create(data);

            // get
            var response = command.Send(executor).AsJToken();

            // setup conditions
            int.TryParse($"{response.SelectToken("code")}", out int codeOut);
            var isCode = codeOut != 0 && codeOut < 400;
            var isFail = $"{response["id"]}" == "-1";

            // exit conditions
            if (isCode && isFail)
            {
                return JToken.Parse(@"{""key"":""" + idOrKey + @"""}");
            }
            else if (!isCode && isFail)
            {
                return JToken.Parse("{}");
            }

            // log
            var key = $"{response.SelectToken("key")}";
            logger?.Debug($"Create-Issue [{key}] = true");

            // comment
            var action = isUpdate ? "updated" : "creted";
            JiraCommandsRepository.AddComment(idOrKey: key, comment: Api.Extensions.Utilities.GetActionSignature(action));

            // results
            return response;
        }

        // extract issue type from issue JSON response
        private static string ExtractIssueType(JToken issue)
        {
            return issue == default ? "-1" : $"{issue.SelectToken("fields.issuetype.name")}";
        }

        // extract project meta data object
        private JToken GetProjectMeta(JiraAuthentication authentication)
        {
            return JiraCommandsRepository
                .CreateMeta(project: authentication.Project)
                .Send(executor)
                .AsJToken()
                .SelectToken("projects")
                .FirstOrDefault();
        }

        // extract transitions for issue
        private IEnumerable<IDictionary<string, string>> DoGetTransitions(string idOrKey)
        {
            // get & verify response
            var transitions = JiraCommandsRepository
                .GetTransitions(idOrKey)
                .Send(executor)
                .AsJObject()
                .SelectToken("transitions");

            // exit conditions
            if (transitions == default || !(transitions is JArray))
            {
                return Array.Empty<IDictionary<string, string>>();
            }

            // build
            var onTransitions = new List<IDictionary<string, string>>();
            foreach (var transition in transitions)
            {
                var onTransition = new Dictionary<string, string>
                {
                    ["id"] = $"{transition["id"]}",
                    ["name"] = $"{transition["name"]}",
                    ["to"] = transition.SelectToken("to.name") == null ? "N/A" : $"{transition.SelectToken("to.name")}"
                };
                onTransitions.Add(onTransition);
            }

            // results
            return onTransitions;
        }
        #endregion
    }
}