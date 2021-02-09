using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using System;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JTokenExtensions
    {
        [Obsolete("Bridge method will be removed after migration to Text.Json")]
        public static JObject AsJObject(this JToken token)
        {
            var json = token == default ? "{}" : $"{token}";
            return JObject.Parse(json);
        }

        [Obsolete("Bridge method will be removed after migration to Text.Json")]
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
