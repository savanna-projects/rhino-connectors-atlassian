/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Rhino.Connectors.AtlassianClients.Contracts
{
    /// <summary>
    /// Represents a Jira issue with its associated fields.
    /// </summary>
    [DataContract]
    internal class JiraIssue
    {
        /// <summary>
        /// Gets or sets the fields of the Jira issue.
        /// </summary>
        [DataMember]
        public JiraFields Fields { get; set; }

        /// <summary>
        /// Represents the fields of a Jira issue.
        /// </summary>
        [DataContract]
        public class JiraFields
        {
            /// <summary>
            /// Gets or sets the project of the Jira issue.
            /// </summary>
            [DataMember]
            public Project Project { get; set; }

            /// <summary>
            /// Gets or sets the summary of the Jira issue.
            /// </summary>
            [DataMember]
            public string Summary { get; set; }

            /// <summary>
            /// Gets or sets the description of the Jira issue.
            /// </summary>
            [DataMember]
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the environment of the Jira issue.
            /// </summary>
            [DataMember]
            public string Environment { get; set; }

            /// <summary>
            /// Gets or sets the issue type of the Jira issue.
            /// </summary>
            [DataMember, JsonPropertyName("issuetype")]
            public IssueType IssueType { get; set; }

            /// <summary>
            /// Gets or sets the priority of the Jira issue.
            /// </summary>
            [DataMember]
            public Priority Priority { get; set; }
        }

        /// <summary>
        /// Represents a Jira project.
        /// </summary>
        [DataContract]
        public class Project
        {
            /// <summary>
            /// Gets or sets the key of the Jira project.
            /// </summary>
            [DataMember]
            public string Key { get; set; }
        }

        /// <summary>
        /// Represents an issue type in Jira.
        /// </summary>
        [DataContract]
        public class IssueType
        {
            /// <summary>
            /// Gets or sets the name of the issue type.
            /// </summary>
            [DataMember]
            public string Name { get; set; }
        }

        /// <summary>
        /// Represents the priority of a Jira issue.
        /// </summary>
        [DataContract]
        public class Priority
        {
            /// <summary>
            /// Gets or sets the ID of the priority.
            /// </summary>
            [DataMember]
            public string Id { get; set; }
        }
    }
}
