/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;

namespace Rhino.Connectors.Xray.Extensions
{
    public static class TestStepExtensions
    {
        /// <summary>
        /// Gets an update request for XRay test case result, under a test execution entity.
        /// </summary>
        /// <param name="testStep">RhinoTestStep by which to create request.</param>
        /// <param name="outcome">Set the step outcome. If not provided, defaults will be assigned.</param>
        public static object GetUpdateRequest(this RhinoTestStep testStep, string outcome)
        {
            // setup
            var inconclusiveStatus = testStep.GetCapability(AtlassianCapabilities.InconclusiveStatus, "ABORTED");

            // set outcome
            var onOutcome = testStep.Actual ? "PASS" : "FAIL";
            if (!string.IsNullOrEmpty(outcome) && outcome != "PASS" && outcome != "FAIL" && outcome != inconclusiveStatus)
            {
                onOutcome = outcome;
            }

            // set request object
            return new
            {
                Id = $"{testStep.Context["runtimeid"]}",
                Status = onOutcome,
                Comment = testStep.Exception == null ? null : $"{{noformat}}{testStep?.Exception}{{noformat}}",
                ActualResult = $"{{noformat}}{testStep.ReasonPhrase}{{noformat}}"
            };
        }
    }
}