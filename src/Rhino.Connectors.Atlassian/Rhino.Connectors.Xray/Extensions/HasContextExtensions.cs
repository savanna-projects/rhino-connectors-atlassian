/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Contracts.Extensions;
using Rhino.Api.Contracts.Interfaces;

using System;
using System.Collections.Generic;

namespace Rhino.Connectors.Xray.Extensions
{
    internal static class HasContextExtensions
    {
        /// <summary>
        /// Gets a value from the capabilities dictionary under the ProviderConfiguration of this RhinoTestCase.
        /// </summary>
        /// <typeparam name="T">The capability type to return.</typeparam>
        /// <param name="onContext">Context dictionary from which to get the capability.</param>
        /// <param name="capability">The capability to get.</param>
        /// <param name="defaultValue">The default value to get if the capability was not found.</param>
        /// <returns>Capability value.</returns>
        public static T GetConnectorCapability<T>(this IHasContext onContext, string capability, T defaultValue = default)
        {
            try
            {
                // setup
                var options = onContext.GetCapability($"{Connector.JiraXRay}:options", new Dictionary<string, object>());

                // setup conditions
                var isKey = options.ContainsKey(capability);
                var isValue = isKey && !string.IsNullOrEmpty($"{options[capability]}");

                // get
                return isValue ? (T)options[capability] : defaultValue;
            }
            catch (Exception e) when (e != null)
            {
                return defaultValue;
            }
        }
    }
}