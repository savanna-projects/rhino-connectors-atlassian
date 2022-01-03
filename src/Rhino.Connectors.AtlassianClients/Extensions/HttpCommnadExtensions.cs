/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Framework;

namespace Rhino.Connectors.AtlassianClients.Extensions
{
    public static class HttpCommnadExtensions
    {
        #region *** Send Command ***
        /// <summary>
        /// Sends an Http command and return the result as JToken instance.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="executor">The JiraCommandsExecutor to use for sending the command.</param>
        /// <returns>JToken with the request results.</returns>
        public static string Send(this HttpCommand command, JiraCommandsExecutor executor)
        {
            return DoSend(command, executor, authentication: default);
        }

        /// <summary>
        /// Sends an Http command and return the result as JToken instance.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="authentication">The JiraAuthentication to use for sending the command.</param>
        /// <returns>JToken with the request results.</returns>
        public static string Send(this HttpCommand command, JiraAuthentication authentication)
        {
            return DoSend(command, executor: default, authentication);
        }

        private static string DoSend(
            HttpCommand command,
            JiraCommandsExecutor executor,
            JiraAuthentication authentication)
        {
            // setup
            var onExecutor = executor ?? new JiraCommandsExecutor(authentication);

            // send
            return onExecutor.SendCommand(command);
        }
        #endregion
    }
}
