using Gravity.Extensions;

using Newtonsoft.Json.Linq;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JTokenExtensions
    {
        public static JObject AsJObject(this JToken token)
        {
            var json = token == default ? "{}" : $"{token}";
            return JObject.Parse(json);
        }

        public static JToken AsJToken(this string token)
        {
            token = string.IsNullOrEmpty(token) ? "{}" : token;
            if (!token.IsJson())
            {
                token = "{}";
            }
            return JToken.Parse(token);
        }
    }
}
