﻿/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Gravity.Services.DataContracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Rhino.Api.Contracts.AutomationProvider;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.Extensions
{
    public static class TestStepExtensions
    {
        /// <summary>
        /// Gets an update request for XRay test case result, under a test execution entity.
        /// </summary>
        /// <param name="testStep">RhinoTestStep by which to create request.</param>
        /// <param name="outcome">The test case out come to set for this RhinoTestStep.</param>
        public static object GetUpdateRequest(this RhinoTestStep testStep, string outcome)
        {
            // set outcome
            var onOutcome = testStep.Actual ? "PASS" : "FAIL";
            if (!string.IsNullOrEmpty(outcome) && !(outcome.Equals("PASS") || outcome.Equals("FAIL")))
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

        // TODO: remove on next Rhino.Api update.
        /// <summary>
        /// Populates screenshots from steps exceptions into test steps context.
        /// </summary>
        /// <param name="testCase">RhinoTestCase object.</param>
        public static void AddExceptionsScreenshot(this RhinoTestCase testCase)
        {
            // extract
            var imagesCollection = ((OrbitResponse)testCase.Context[ContextEntry.OrbitResponse])
                .OrbitRequest
                .Exceptions
                .Where(i => !string.IsNullOrEmpty(i.Screenshot) && i.Action != ActionType.Assert);

            // apply
            foreach (var image in imagesCollection)
            {
                if (image.ActionReference > testCase.Steps.Count())
                {
                    break;
                }
                testCase.Steps.ElementAt(image.ActionReference).Context["screenshot"] = image.Screenshot;
            }
        }
    }
}