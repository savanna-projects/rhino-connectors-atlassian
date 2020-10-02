/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;

namespace Rhino.Connectors.AtlassianClients.Contracts
{
    [DataContract]
    public class HttpCommand
    {
        /// <summary>
        /// Gets or sets the request content type. Default is "application/json";
        /// </summary>
        [DataMember]
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the method of this RavenCommand
        /// </summary>
        [DataMember]
        public HttpMethod Method { get; set; }

        /// <summary>
        /// Gets or sets the route of this RavenCommand
        /// </summary>
        [DataMember]
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets data (payload) of this command
        /// </summary>
        [DataMember]
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets additional headers to send with this command.
        /// </summary>
        [DataMember]
        public IDictionary<string, string> Headers { get; set; }
    }
}