/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System.Runtime.Serialization;

namespace Rhino.Connectors.AtlassianClients.Contracts
{
    /// <summary>
    /// Constants for XRay Connector capabilities
    /// </summary>
    [DataContract]
    public static class AtlassianCapabilities
    {
        /// <summary>
        /// Test case issue type capability, if not set "Test" is the default.
        /// </summary>
        [DataMember]
        public const string TestType = "testType";

        /// <summary>
        /// Test set issue type capability, if not set "Test Set" is the default.
        /// </summary>
        [DataMember]
        public const string SetType = "setType";

        /// <summary>
        /// Test preconditions issue type capability, if not set "Pre-Condition" is the default.
        /// </summary>
        [DataMember]
        public const string PreconditionsType = "preconditionsType";

        /// <summary>
        /// Test plan issue type capability, if not set "Test Plan" is the default.
        /// </summary>
        [DataMember]
        public const string PlanType = "planType";

        /// <summary>
        /// Test execution issue type capability, if not set "Test Plan" is the default.
        /// </summary>
        [DataMember]
        public const string ExecutionType = "executionType";

        /// <summary>
        /// Bug issue type capability, if not set "Bug" is the default.
        /// </summary>
        [DataMember]
        public const string BugType = "bugType";

        /// <summary>
        /// Holds a boolean value rather or not to create Test Execution entity when running tests.
        /// </summary>
        [DataMember]
        public const string DryRun = "dryRun";

        /// <summary>
        /// Holds test plans keys. If set, when test is created it will also be associated with these test plans.
        /// </summary>
        [DataMember]
        public const string TestPlans = "testPlans";

        /// <summary>
        /// The Jira API version to use when executing requests against Jira API. If not specified, "latest" will be used.
        /// </summary>
        [DataMember]
        public const string JiraApiVersion = "jiraApiVersion";

        /// <summary>
        /// The number of parallel requests which will send when using Jira API. If not specified, 4 will be used.
        /// </summary>
        [DataMember]
        public const string BucketSize = "bucketSize";
    }
}
