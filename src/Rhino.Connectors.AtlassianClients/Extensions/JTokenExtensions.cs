﻿using Gravity.Extensions;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class JTokenExtensions
    {
        public static T GetOrDefault<T>(this IDictionary<string, object> data, string key, T defaultValue)
        {
            if (!data.ContainsKey(key))
            {
                return defaultValue;
            }
            return data[key] == default ? default : (T)data[key];
        }

        public static JObject AsJObject(this JToken token)
        {
            try
            {
                var json = token == default ? "{}" : $"{token}";
                return JObject.Parse(json);
            }
            catch (Exception)
            {
                return JObject.Parse("{}");
            }
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