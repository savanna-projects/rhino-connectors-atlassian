using Newtonsoft.Json.Linq;

using Rhino.Connectors.AtlassianClients;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.Framework
{
    public static class JiraExtenstions
    {
        // constants
        private const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

        public static string GetFieldId(this JiraClient jiraClient, string issueType, string fieldName)
        {
            // setup
            var issue = jiraClient
                .ProjectMeta["issuetypes"]
                .FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(issueType, Compare));

            return issue
                .SelectToken("fields")
                .Children()
                .SelectMany(i => i)
                .FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(fieldName, Compare))?
                .SelectToken("fieldId")?
                .ToString();
        }

        public static IDictionary<string, object> GetCustomFieldsWithValues(
            this JiraClient jiraClient, string type, IDictionary<string, object> customFields)
        {
            // setup
            var issue = jiraClient
                .ProjectMeta["issuetypes"]
                .FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(type, Compare));

            // not found
            if (issue == default)
            {
                return new Dictionary<string, object>();
            }

            // setup
            var fields = new Dictionary<string, object>();

            // iterate
            foreach (var item in customFields)
            {
                var field = issue
                    .SelectToken("fields")
                    .Children()
                    .SelectMany(i => i)
                    .FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(item.Key, Compare));

                // not found
                if (field == null)
                {
                    continue;
                }

                // setup
                var id = $"{field.SelectToken("fieldId")}";
                var values = field.SelectToken("allowedValues");
                var schemaType = $"{field.SelectToken("schema.type")}";

                // set value
                if (values == null)
                {
                    fields[id] = schemaType.Equals("array", Compare) ? new[] { item.Value } : item.Value;
                    continue;
                }
                var value = jiraClient.GetAllowedValueId(type, $"..{id}", $"{item.Value}");

                // get
                fields[id] = schemaType.Equals("array", Compare)
                    ? new[] { new Dictionary<string, object> { ["id"] = value } }
                    : new Dictionary<string, object> { ["id"] = value };
            }

            // get
            return fields;
        }
    }
}
