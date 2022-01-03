#pragma warning disable
using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Cloud;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rhino.Connectors.Xray.UnitTests
{
    [TestClass]
    public class OnGoing
    {
        [TestMethod]
        public void Test()
        {
            var configuration = new RhinoConfiguration
            {
                DriverParameters = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["driver"] = "ChromeDriver",
                        ["driverBinaries"] = @"D:\AutomationEnvironment\WebDrivers"
                    }
                },
                Authentication = new()
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                //TestsRepository = new[]
                //{
                //    "RP-515"
                //},
                TestsRepository = new[]
                {
                    "[test-id] RP-60\r\n" +
                    "[test-scenario] Open a Web Site\r\n" +
                    "[test-priority] high \r\n" +
                    "[test-actions]\r\n" +
                    "1. go to url {https://gravitymvctestapplication.azurewebsites.net/}\r\n" +
                    "2. wait {1000}\r\n" +
                    "3. register parameter {{$ --name:parameter --scope:session}} take {//a}  \r\n" +
                    "4. close browser\r\n" +
                    "[test-expected-results]\r\n" +
                    "[1] {url} match {azure}"
                },
                ConnectorConfiguration = new()
                {
                    Collection = "http://localhost:8082",
                    UserName = "admin",
                    Password = "admin",
                    Project = "RP",
                    Connector = "ConnectorXrayText",
                    DryRun = false,
                    BugManager = true
                },
                //Capabilities = new Dictionary<string, object>
                //{
                //    ["customFields"] = new Dictionary<string, object>
                //    {
                //        ["Fixed drop"] = "1.0.0.0",
                //        ["Fix Version/s"] = "1.0.0.0",
                //        ["No Field"] = 1,
                //        ["Test Checkbox"] = "Option 2",
                //        ["Multi-Branch"] = "No",
                //        ["Story Points"] = 10
                //    },
                //    ["syncFields"] = new[] { "Sync Fields" }
                //},
                //ConnectorConfiguration = new()
                //{
                //    Collection = "https://rhinoapi.atlassian.net",
                //    UserName = "rhino.api@gmail.com",
                //    Password = "HKt4qIcoIbUUFqu1hSA88B41",
                //    Project = "RP",
                //    Connector = RhinoConnectors.JiraXryCloud,
                //    DryRun = false,
                //    BugManager = true
                //},
                EngineConfiguration = new()
                {
                    ReturnPerformancePoints = true,
                    RetrunExceptions = true,
                    ReturnEnvironment = true
                },
                ScreenshotsConfiguration = new()
                {
                    ReturnScreenshots = true,
                    KeepOriginal = false,
                    OnExceptionOnly = false
                }
            };

            var connector = new Rhino.Connectors.Xray.Text.XrayTextConnector(configuration);
            var testCases = new Rhino.Api.Parser.RhinoTestCaseFactory().GetTestCases(configuration.TestsRepository.ToArray());
            //connector.ProviderManager.CreateTestCase(testCases.ElementAt(0));
            configuration.Execute(Utilities.Types);
        }
    }
}
