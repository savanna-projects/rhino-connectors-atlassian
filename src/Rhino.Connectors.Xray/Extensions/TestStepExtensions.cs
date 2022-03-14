/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;

using System.Linq;

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
            var format =
                "Class:   {0}  \n" +
                "Message: {1}  \n" +
                "Method:  {2}  \n" +
                "Type:    {3}";
            var comments = testStep?.Exceptions.Select(i => string.Format(format, i.Class, i.Message, i.Method, i.Type));
            var comment = !testStep.HaveExceptions()
                ? string.Empty
                : string.Join("  \n  \n", comments);
            return new
            {
                Id = $"{testStep.Context["runtimeid"]}",
                Status = onOutcome,
                Comment = string.IsNullOrEmpty(comment) ? null : $"{{noformat}}{comment}{{noformat}}",
                ActualResult = string.IsNullOrEmpty(testStep.ReasonPhrase) ? null : $"{{noformat}}{testStep.ReasonPhrase}{{noformat}}"
            };
        }
    }
}
