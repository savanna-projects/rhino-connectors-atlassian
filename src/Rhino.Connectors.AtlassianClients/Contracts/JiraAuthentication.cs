/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Rhino.Connectors.AtlassianClients.Contracts
{
    /// <summary>
    /// Contract for describing Jira authentication information.
    /// </summary>
    [DataContract]
    public class JiraAuthentication
    {
        /// <summary>
        /// The server address or the default collection address.
        /// </summary>
        [DataMember]
        public string Collection { get; set; } = string.Empty;

        /// <summary>
        /// Project name under which you want to execute tests.
        /// </summary>
        [DataMember]
        public string Project { get; set; } = string.Empty;

        /// <summary>
        /// A valid ALM user name with testing & test runs permissions (on some ALM administrator permissions are needed).
        /// </summary>
        [DataMember]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// A valid ALM password.
        /// </summary>
        [DataMember]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Will use user & password to generate operating system credential to connect to the server
        /// enable unsecured connection.
        /// </summary>
        [DataMember]
        public bool AsOsUser { get; set; } = false;

        /// <summary>
        /// Gets or sets additional capabilities to configure connection and integration with
        /// the ALM.
        /// </summary>
        /// <remarks>This field is for allowing better flexibility when you implement a connector.</remarks>
        [DataMember]
        public IDictionary<string, object> Capabilities { get; set; } = new Dictionary<string, object>();
    }
}