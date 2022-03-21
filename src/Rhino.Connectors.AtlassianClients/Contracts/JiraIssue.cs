using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Rhino.Connectors.AtlassianClients.Contracts
{
    [DataContract]
    internal class JiraIssue
    {
        [DataMember]
        public JiraFields Fields { get; set; }

        [DataContract]
        internal class JiraFields
        {
            [DataMember]
            public Project Project { get; set; }

            [DataMember]
            public string Summary { get; set; }

            [DataMember]
            public string Description { get; set; }

            [DataMember]
            public string Environment { get; set; }

            [DataMember, JsonPropertyName("issuetype")]
            public IssueType IssueType { get; set; }

            [DataMember]
            public Priority Priority { get; set; }
        }

        [DataContract]
        internal class Project
        {
            [DataMember]
            public string Key { get; set; }
        }

        [DataContract]
        internal class IssueType
        {
            [DataMember]
            public string Name { get; set; }
        }

        [DataContract]
        internal class Priority
        {
            [DataMember]
            public string Id { get; set; }
        }
    }
}
