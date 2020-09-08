/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Runtime.Serialization;

namespace Rhino.Connectors.Xray.Contracts
{
    /// <summary>
    /// Constants for XRay Connector capabilities
    /// </summary>
    [DataContract]
    public static class XrayCapabilities
    {
        /// <summary>
        /// Test case issue type capability, if not set "Test" is the default.
        /// </summary>
        [DataMember]
        public static string TestType = "testType";

        /// <summary>
        /// Test set issue type capability, if not set "Test Set" is the default.
        /// </summary>
        [DataMember]
        public static string SetType = "setType";

        /// <summary>
        /// Test preconditions issue type capability, if not set "Pre-Condition" is the default.
        /// </summary>
        [DataMember]
        public static string PreconditionsType = "preconditionsType";

        /// <summary>
        /// Test plan issue type capability, if not set "Test Plan" is the default.
        /// </summary>
        [DataMember]
        public static string PlanType = "planType";
    }
}
