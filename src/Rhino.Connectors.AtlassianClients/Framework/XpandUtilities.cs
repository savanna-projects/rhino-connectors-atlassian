using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Rhino.Connectors.AtlassianClients.Framework
{
    public static class XpandUtilities
    {
        private static string s_interactiveJwt;
        private static readonly HttpClient s_httpClient = new();

        public static async Task<string> GetJwt(JiraAuthentication authentication, string issue)
        {
            try
            {
                var response = (await GetInteractiveIssueToken(authentication, issue)).ConvertToJToken();
                var options = response.SelectTokens("..options").FirstOrDefault()?.ToString();
                var token = JToken.Parse(options).SelectToken("contextJwt")?.ToString();

                if (string.IsNullOrEmpty(token))
                {
                    return s_interactiveJwt;
                }

                s_interactiveJwt = token;
                return s_interactiveJwt;
            }
            catch (Exception)
            {
                return s_interactiveJwt;
            }
        }

        public static async Task<string> GetInteractiveIssueToken(JiraAuthentication authentication, string issue)
        {
            var data = Assembly
                .GetExecutingAssembly()
                .ReadEmbeddedResource("get_interactive_token.txt")
                .Replace("[project-key]", authentication.Project)
                .Replace("[issue-key]", issue);

            var parameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authentication.Username}:{authentication.Password}"));
            var authorization = new AuthenticationHeaderValue("Basic", parameter);
            var route = "/rest/gira/1/?operation=issueViewInteractiveQuery";
            var url = authentication.Collection.TrimEnd('/') + route;
            var request = new HttpRequestMessage
            {
                Content = new StringContent(data, Encoding.UTF8, "application/json"),
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };
            request.Headers.Authorization = authorization;

            var response = await s_httpClient.SendAsync(request);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync()
                : "{}";
        }
    }
}
