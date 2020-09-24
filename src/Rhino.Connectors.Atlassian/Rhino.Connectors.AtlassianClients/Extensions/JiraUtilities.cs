/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 * 
 * WORK ITEMS
 * TODO: reuse code for getting the base http request object with authentication
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Rhino.Connectors.AtlassianClients.Contracts;

using System;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JiraUtilities
    {
        // constants        
        private const string IssueFormat = "/rest/api/{0}/issue";

        /// <summary>
        /// Gets "application/json" media type constant
        /// </summary>
        public const string MediaType = "application/json";

        /// <summary>
        /// Gets the HttpClient client used by this JiraClient.
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
        /// Gets a request for uploading an attachment into Jira issue.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="path">Full path of the file which will be uploaded into the issue.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage AddAttachmentRequest(JiraAuthentication authentication, string idOrKey, string path)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = string.Format(IssueFormat, apiVersion);

            // setup
            var bytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            var endpoint = new Uri(string.Format("{0}/{1}/attachments", baseAddress + route, idOrKey));

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

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

        /// <summary>
        /// Gets a request for delete an attachment from Jira issue.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="attachment">Attachment id by which to delete attachment.</param>
        /// <returns>Delete request ready for posting.</returns>
        public static HttpRequestMessage DeleteAttachmentRequest(JiraAuthentication authentication, string attachment)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"/rest/api/{apiVersion}/attachment";

            // setup
            var endpoint = $"{baseAddress}/{route}/attachment/{attachment}";

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // get
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for searching Jira by using query string JQL.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="jql">JQL to search by.</param>
        /// <returns>Get request ready for posting.</returns>
        public static HttpRequestMessage GetByJqlRequest(JiraAuthentication authentication, string jql)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"/rest/api/{apiVersion}/search?jql={jql}";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for getting Jira issue.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="queryString">Additional query parameters to pass with the request.</param>
        /// <returns>Get request ready for posting.</returns>
        public static HttpRequestMessage GetRequest(JiraAuthentication authentication, string idOrKey, string queryString)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"{string.Format(IssueFormat, apiVersion)}/{idOrKey}{queryString}";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for getting Jira issue transitions.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <returns>Get request ready for posting.</returns>
        public static HttpRequestMessage GetTransitionsRequest(JiraAuthentication authentication, string idOrKey)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"{string.Format(IssueFormat, apiVersion)}/{idOrKey}/transitions?expand=transitions.fields";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for creating Jira issue.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="requestBody">The request JSON payload to send in order to create the issue.</param>
        /// <returns>Post/Put request ready for posting.</returns
        public static HttpRequestMessage CreateOrUpdateRequst(JiraAuthentication authentication, string idOrKey, string requestBody)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = string.Format(IssueFormat, apiVersion);
            var endpoint = string.IsNullOrEmpty(idOrKey) ? baseAddress + route : baseAddress + route + $"/{idOrKey}";

            // setup: request
            var method = string.IsNullOrEmpty(idOrKey) ? HttpMethod.Post : HttpMethod.Put;
            var requestMessage = new HttpRequestMessage(method, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // setup: request data
            requestMessage.Content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for getting project meta data.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <returns>Get request ready for posting.</returns
        public static HttpRequestMessage GetMetaRequest(JiraAuthentication authentication)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"{string.Format(IssueFormat, apiVersion)}/createmeta" +
                "?projectKeys=" + authentication.Project +
                "&expand=projects.issuetypes.fields";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for creating link between 2 issues.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="linkType">The name of the link type to create (e.g. Blocks).</param>
        /// <param name="inward">The key of the inward issue (i.e. the issue which blocks).</param>
        /// <param name="outward">The key of the outward issue (i.e. the issue which is blocked by).</param>
        /// <param name="comment">Comment to create for this link.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage CreateLinkRequest(
            JiraAuthentication authentication,
            string linkType,
            string inward,
            string outward,
            string comment)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"/rest/api/{apiVersion}/issueLink";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // setup: request data
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
                    Body = comment
                }
            };
            var requestBody = JsonConvert.SerializeObject(requestObjt, JsonSettings);
            requestMessage.Content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a request for adding a comment to issue.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="comment">Comment to apply.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage CreateCommentRequest(JiraAuthentication authentication, string idOrKey, string comment)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"{string.Format(IssueFormat, apiVersion)}/{idOrKey}";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // setup: request data
            var requestObjt = new
            {
                Update = new
                {
                    Comment = new[]
                    {
                        new
                        {
                            Add = new
                            {
                                Body = comment
                            }
                        }
                    }
                }
            };
            var requestBody = JsonConvert.SerializeObject(requestObjt, JsonSettings);
            requestMessage.Content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }

        /// <summary>
        /// Gets a generic get request.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="route">Route to apply to JiraAuthentication.Collection (does not include /api/[version]).</param>
        /// <returns>Get request ready for posting.</returns>
        public static HttpRequestMessage GenericGetRequest(JiraAuthentication authentication, string route)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var onRoute = route;
            var endpoint = baseAddress + onRoute;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        #region *** Generic Request   ***
        /// <summary>
        /// Gets a generic post request.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="route">Route to apply to JiraAuthentication.Collection (does not include /api/[version]).</param>
        /// <param name="payload">The payload object to post with this request.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage GenericPostRequest(JiraAuthentication authentication, string route, object payload)
        {
            // setup
            var onPayload = JsonConvert.SerializeObject(payload, JsonSettings);

            // post
            return GenericPostOrPutRequest(authentication, HttpMethod.Post, route, onPayload);
        }

        /// <summary>
        /// Gets a generic post request.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="route">Route to apply to JiraAuthentication.Collection (does not include /api/[version]).</param>
        /// <param name="payload">The payload object to post with this request.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage GenericPostRequest(JiraAuthentication authentication, string route, string payload)
        {
            return GenericPostOrPutRequest(authentication, HttpMethod.Post, route, payload);
        }

        /// <summary>
        /// Gets a generic post request.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="route">Route to apply to JiraAuthentication.Collection (does not include /api/[version]).</param>
        /// <param name="payload">The payload object to post with this request.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage GenericPutRequest(JiraAuthentication authentication, string route, object payload)
        {
            //setup
            var onPayload = JsonConvert.SerializeObject(payload, JsonSettings);

            // put
            return GenericPostOrPutRequest(authentication, HttpMethod.Put, route, onPayload);
        }

        /// <summary>
        /// Gets a generic post request.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="route">Route to apply to JiraAuthentication.Collection (does not include /api/[version]).</param>
        /// <param name="payload">The payload object to post with this request.</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage GenericPutRequest(JiraAuthentication authentication, string route, string payload)
        {
            return GenericPostOrPutRequest(authentication, HttpMethod.Put, route, payload);
        }

        private static HttpRequestMessage GenericPostOrPutRequest(
            JiraAuthentication authentication,
            HttpMethod method,
            string route,
            string payload)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(method, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // set content
            requestMessage.Content = new StringContent(content: payload, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }
        #endregion

        #region *** Create Transition ***
        /// <summary>
        /// Gets a request for creating issue transition.
        /// </summary>
        /// <param name="authentication">Authentication information to send with this request.</param>
        /// <param name="idOrKey">Jira issue id or issue key.</param>
        /// <param name="transitionId">The ID of the transition (you can use GetTransitions method to get the transition ID).</param>
        /// <param name="resolution">The resolution to pass with the transition.</param>
        /// <param name="comment">A comment to add when posting transition</param>
        /// <returns>Post request ready for posting.</returns>
        public static HttpRequestMessage CreateTransitionRequest(
            JiraAuthentication authentication,
            string idOrKey,
            string transitionId,
            string resolution,
            string comment)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var route = $"{string.Format(IssueFormat, apiVersion)}/{idOrKey}/transitions";
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // setup: request data
            var requestBody = GetTransitionRequestBody(authentication.User, transitionId, resolution, comment);
            requestMessage.Content = new StringContent(content: requestBody, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }

        private static string GetTransitionRequestBody(string assignee, string transitionId, string resolution, string comment)
        {
            var requestBody = new
            {
                Update = new
                {
                    Comment = new[]
                    {
                        new
                        {
                            Add = new
                            {
                                Body = comment
                            }
                        }
                    }
                },
                Fields = new
                {
                    Assignee = new
                    {
                        Name = assignee
                    },
                    Resolution = new
                    {
                        Name = resolution
                    }
                },
                Transition = new
                {
                    Id = transitionId
                }
            };
            return JsonConvert.SerializeObject(requestBody, JsonSettings);
        }
        #endregion

        // UTILITIES
        private static string GetBaseAddress(JiraAuthentication authentication)
        {
            return authentication?.Collection.EndsWith("/") == true
                ? authentication?.Collection.Substring(0, HttpClient.BaseAddress.AbsoluteUri.LastIndexOf('/'))
                : authentication?.Collection;
        }
    }
}