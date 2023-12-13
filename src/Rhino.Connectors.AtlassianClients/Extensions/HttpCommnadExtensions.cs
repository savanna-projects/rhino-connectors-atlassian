/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Framework;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    /// <summary>
    /// Extension methods for sending HTTP commands to Jira.
    /// </summary>
    public static class HttpCommnadExtensions
    {
        #region *** Send Command ***
        /// <summary>
        /// Sends an HTTP command to Jira using the provided executor and default authentication.
        /// </summary>
        /// <param name="command">The HTTP command to send.</param>
        /// <param name="executor">The JiraCommandsExecutor to use. If not provided, a new one with default authentication will be created.</param>
        /// <returns>The response from the Jira server.</returns>
        public static string Send(this HttpCommand command, JiraCommandsExecutor executor)
        {
            return Send(command, authentication: default, executor);
        }

        /// <summary>
        /// Sends an HTTP command to Jira using the provided authentication and default executor.
        /// </summary>
        /// <param name="command">The HTTP command to send.</param>
        /// <param name="authentication">The JiraAuthentication to use. If not provided, default authentication will be used.</param>
        /// <returns>The response from the Jira server.</returns>
        public static string Send(this HttpCommand command, JiraAuthentication authentication)
        {
            return Send(command, authentication, executor: default);
        }

        /// <summary>
        /// Sends an HTTP command to Jira using the provided authentication and executor.
        /// </summary>
        /// <param name="command">The HTTP command to send.</param>
        /// <param name="authentication">The JiraAuthentication to use. If not provided, default authentication will be used.</param>
        /// <param name="executor">The JiraCommandsExecutor to use. If not provided, a new one with the specified authentication will be created.</param>
        /// <returns>The response from the Jira server.</returns>
        private static string Send(
            HttpCommand command, JiraAuthentication authentication, JiraCommandsExecutor executor)
        {
            // Create a new executor if not provided
            var onExecutor = executor ?? new JiraCommandsExecutor(authentication);

            // Send the command and return the response
            return onExecutor.SendCommand(command);
        }
        #endregion
    }
}
