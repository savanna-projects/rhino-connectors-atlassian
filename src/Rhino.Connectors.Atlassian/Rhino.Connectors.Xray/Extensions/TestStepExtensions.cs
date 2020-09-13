/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.AutomationProvider;

namespace Rhino.Connectors.Xray.Extensions
{
    public static class TestStepExtensions
    {
        /// <summary>
        /// Gets an update request for XRay test case result, under a test execution entity.
        /// </summary>
        /// <param name="testStep">RhinoTestStep by which to create request.</param>
        public static object GetUpdateRequest(this RhinoTestStep testStep)
        {
            // set outcome
            var onOutcome = testStep.Actual ? "PASS" : "FAIL";

            // set request object
            return new
            {
                Id = $"{testStep.Context["runtimeid"]}",
                Status = onOutcome,
                Comment = testStep.Exception == null ? null : $"{{noformat}}{testStep?.Exception}{{noformat}}",
                ActualResult = testStep.ReasonPhrase
            };
        }
    }
}