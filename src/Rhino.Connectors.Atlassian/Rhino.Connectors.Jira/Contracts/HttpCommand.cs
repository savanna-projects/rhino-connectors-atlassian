/*
 * CHANGE LOG - keep only last 5 threads
 */
using System.Net.Http;
using System.Runtime.Serialization;

namespace Rhino.Connectors.Xray.Cloud.Contracts
{
    /// <summary>
    /// Contract for describing HTTP command components.
    /// </summary>
    [DataContract]
    internal class HttpCommand
    {
        /// <summary>
        /// Standard HTTP method.
        /// </summary>
        [DataMember]
        public HttpMethod Type { get; set; } = HttpMethod.Get;

        /// <summary>
        /// The HTTP command to send (will be the route w/o parameters).
        /// </summary>
        [DataMember]
        public string Command { get; set; } = "/";

        /// <summary>
        /// Request body object (if needed).
        /// </summary>
        [DataMember]
        public object Data { get; set; }
    }
}