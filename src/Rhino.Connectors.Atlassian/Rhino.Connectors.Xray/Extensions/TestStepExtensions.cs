/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 * 
 * WORK ITEMS
 * TODO: replace ABORTED with capability
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
        /// <param name="outcome">Set the step outcome. If not provided, defaults will be assigned.</param>
        public static object GetUpdateRequest(this RhinoTestStep testStep, string outcome)
        {
            // set outcome
            var onOutcome = testStep.Actual ? "PASS" : "FAIL";
            if (!string.IsNullOrEmpty(outcome) && outcome != "PASS" && outcome != "FAIL" && outcome != "ABORTED")
            {
                onOutcome = outcome;
            }

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