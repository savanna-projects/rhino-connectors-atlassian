/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Abstraction.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Rhino.Connectors.AtlassianClients.Framework
{
    /// <summary>
    /// Client for XRay Server (On Premise) API
    /// </summary>
    public class JiraCommandsExecutor
    {
        // members
        private readonly JiraAuthentication authentication;
        private readonly ILogger logger;
        private readonly int bucketSize;

        #region *** Constructors ***
        /// <summary>
        /// Creates a new instance of JiraCommandExecutor.
        /// </summary>
        /// <param name="authentication">JiraAuthentication instance to use with this JiraCommandExecutor.</param>
        public JiraCommandsExecutor(JiraAuthentication authentication)
            : this(authentication, logger: default)
        { }

        /// <summary>
        /// Creates a new instance of JiraCommandExecutor.
        /// </summary>
        /// <param name="authentication">JiraAuthentication instance to use with this JiraCommandExecutor.</param>
        /// <param name="logger">Logger implementation for this JiraCommandExecutor.</param>
        public JiraCommandsExecutor(JiraAuthentication authentication, ILogger logger)
        {
            this.authentication = authentication;
            this.logger = logger != default ? logger.CreateChildLogger(nameof(JiraCommandsExecutor)) : logger;
            bucketSize = authentication.GetCapability(ProviderCapability.BucketSize, 4);
        }
        #endregion

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
        /// Adds one or more attachments to an issue.
        /// </summary>
        /// <param name="idOrKey">The ID or key of the issue that attachments are added to.</param>
        /// <param name="files">Files to attach (full path and content type).</param>
        public void AddAttachments(string idOrKey, params (string Path, string ContentType)[] files)
        {
            // setup
            var request = AddAttachmentsRequest(authentication, idOrKey, files);

            // post
            SendRequest(request);
        }

        /// <summary>
        /// Sends an Http command and return the result as JToken instance.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <returns>JToken with the request results.</returns>
        public string SendCommand(HttpCommand command)
        {
            return DoSendCommand(command);
        }

        /// <summary>
        /// Sends an Http command and return the result as JToken instance.
        /// </summary>
        /// <param name="commands">A collection of commands to send.</param>
        /// <returns>A collection of JToken with the request results.</returns>
        public IEnumerable<string> SendCommands(IEnumerable<HttpCommand> commands)
        {
            // setup
            var results = new ConcurrentBag<string>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = bucketSize };

            // execute
            Parallel.ForEach(commands, options, command => results.Add(DoSendCommand(command)));

            // get
            return results;
        }

        private string DoSendCommand(HttpCommand command)
        {
            // constants
            const StringComparison Comapre = StringComparison.OrdinalIgnoreCase;

            // setup
            var methods = GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(i => i.GetCustomAttribute<DescriptionAttribute>() != null);

            var method = methods
                .FirstOrDefault(i => i.GetCustomAttribute<DescriptionAttribute>().Description.Equals(command.Method.Method, Comapre));

            // send conditions
            if (method != default)
            {
                return (string)method.Invoke(this, new object[] { command });
            }

            // fail conditions
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                ReasonPhrase = $"Get-ExecutionMethod -By [{command.Method.Method}] = false",
                Content = new StringContent(string.Empty)
            };
            return GetCustomResponse(response);
        }

        #region *** Requests Factory ***
        [Description("GET")]
        private string Get(HttpCommand command)
        {
            // setup
            var request = GenericGetRequest(authentication, command.Route);

            // factor
            request = FactorXpandMessage(request, command);

            // get
            return SendRequest(request);
        }

        [Description("POST")]
        private string Post(HttpCommand command)
        {
            // setup
            var request = GenericPostRequest(authentication, command.Route, command.Data);

            // factor
            request = FactorXpandMessage(request, command);

            // post
            return SendRequest(request);
        }

        [Description("PUT")]
        private string Put(HttpCommand command)
        {
            // setup
            var request = GenericPutRequest(authentication, command.Route, command.Data);

            // factor
            request = FactorXpandMessage(request, command);

            // post
            return SendRequest(request);
        }

        [Description("DELETE")]
        private string Delete(HttpCommand command)
        {
            // setup
            var request = GenericDeleteRequest(authentication, command.Route);

            // factor
            request = FactorXpandMessage(request, command);

            // get
            return SendRequest(request);
        }
        #endregion

        // UTILITIES
        // factor message for internal xray.cloud.xpand-it.com endpoint
        private HttpRequestMessage FactorXpandMessage(HttpRequestMessage message, HttpCommand command)
        {
            // constants
            const string Xacpt = "X-acpt";

            // exit conditions
            if (command.Headers == default)
            {
                return message;
            }
            else if (!command.Headers.ContainsKey(Xacpt))
            {
                return message;
            }
            else if (string.IsNullOrEmpty(command.Headers[Xacpt]))
            {
                return message;
            }

            // TODO: handle "too many requests" exception code 429
            // extract
            var response = JiraCommandsRepository.GetToken(authentication.Project, command.Headers[Xacpt]).Send(this).AsJToken();
            var options = $"{response.SelectTokens("..options").FirstOrDefault()}";
            var token = $"{JToken.Parse(options).SelectToken("contextJwt")}";

            // logging
            if (string.IsNullOrEmpty(token))
            {
                logger.Error("Get-XpandToken" +
                    $"-Route [{command.Route}]" +
                    $"-Project [{authentication.Project}]" +
                    $"-Key [{command.Headers[Xacpt]}] = false");
            }

            // apply
            message.Headers.Add(Xacpt, token);
            message.RequestUri = new Uri($"https://xray.cloud.xpand-it.com{command.Route}");

            // get
            return message;
        }

        // send an HTTP request
        private string SendRequest(HttpRequestMessage request)
        {
            // get
            var response = HttpClient.SendAsync(request).GetAwaiter().GetResult();

            // logging
            var message = "Send-Request " +
                $"-Url [{request.RequestUri.AbsoluteUri}] " +
                $"-Method [{request.Method.Method}] = " +
                $"-Code [{response.StatusCode}] " +
                $"-Reason [{response.ReasonPhrase}]";

            // parse
            if (!response.IsSuccessStatusCode)
            {
                logger?.Error(message);
                return GetCustomResponse(response);
            }

            // parse
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            logger?.Info(message);

            // get
            return string.IsNullOrEmpty(responseBody) ? GetCustomResponse(response) : responseBody;
        }

        private static string GetCustomResponse(HttpResponseMessage response)
        {
            // setup
            var resopnseObjt = new
            {
                Code = response.StatusCode,
                Reason = response.ReasonPhrase,
                Body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                Id = "-1"
            };

            // get
            return JsonConvert.SerializeObject(resopnseObjt, JsonSettings);
        }

        // gets a generic get request.
        private static HttpRequestMessage GenericGetRequest(JiraAuthentication authentication, string route)
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

        // gets a generic post request.
        private static HttpRequestMessage GenericPostRequest(JiraAuthentication authentication, string route, object data)
        {
            //setup
            var onPayload = JsonConvert.SerializeObject(data, JsonSettings);
            if (data is JToken || data is string)
            {
                onPayload = $"{data}";
            }

            // post
            return GenericPostOrPutRequest(authentication, HttpMethod.Post, route, onPayload);
        }

        // gets a generic get request.
        private static HttpRequestMessage GenericDeleteRequest(JiraAuthentication authentication, string route)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var onRoute = route;
            var endpoint = baseAddress + onRoute;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // results
            return requestMessage;
        }

        // gets a generic put request.
        private static HttpRequestMessage GenericPutRequest(JiraAuthentication authentication, string route, object data)
        {
            //setup
            var onPayload = JsonConvert.SerializeObject(data, JsonSettings);
            if (data is JToken || data is string)
            {
                onPayload = $"{data}";
            }

            // put
            return GenericPostOrPutRequest(authentication, HttpMethod.Put, route, onPayload);
        }

        private static HttpRequestMessage GenericPostOrPutRequest(
            JiraAuthentication authentication,
            HttpMethod method,
            string route,
            string data)
        {
            // address
            var baseAddress = GetBaseAddress(authentication);
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(method, endpoint);
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();

            // set content
            requestMessage.Content = new StringContent(content: data, Encoding.UTF8, MediaType);

            // results
            return requestMessage;
        }

        // Gets a request for uploading an attachment into Jira issue.
        private static HttpRequestMessage AddAttachmentsRequest(
            JiraAuthentication authentication,
            string idOrKey,
            params (string Path, string ContentType)[] files)
        {
            // setup
            var baseAddress = GetBaseAddress(authentication);
            var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            var urlPath = $"{baseAddress}/rest/api/{apiVersion}/issue/{idOrKey}/attachments";

            // build request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;
            requestMessage.Headers.Authorization = authentication.GetAuthenticationHeader();
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            // build multi part content
            var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

            // build file content
            foreach (var (Path, ContentType) in files)
            {
                var fileInfo = new FileInfo(Path);
                var fileContents = File.ReadAllBytes(fileInfo.FullName);
                var byteArrayContent = new ByteArrayContent(fileContents);
                byteArrayContent.Headers.Add("Content-Type", ContentType);
                byteArrayContent.Headers.Add("X-Atlassian-Token", "no-check");
                multiPartContent.Add(byteArrayContent, "file", fileInfo.Name);
            }

            // set request content
            requestMessage.Content = multiPartContent;

            // get
            return requestMessage;
        }

        // normalize base address
        private static string GetBaseAddress(JiraAuthentication authentication)
        {
            return authentication?.Collection.EndsWith("/") == true
                ? authentication?.Collection.Substring(0, authentication.Collection.LastIndexOf('/'))
                : authentication?.Collection;
        }
    }
}