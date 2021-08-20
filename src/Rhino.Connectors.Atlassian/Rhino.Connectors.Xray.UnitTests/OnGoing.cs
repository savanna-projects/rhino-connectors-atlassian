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
                TestsRepository = new[] { "RP-1" },
                ConnectorConfiguration = new()
                {
                    Collection = "http://localhost:8082",
                    UserName = "admin",
                    Password = "admin",
                    Project = "RP",
                    Connector = Connector.JiraXRay,
                    DryRun = false,
                    BugManager = false
                },
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
            configuration.Execute(Utilities.Types);
        }
    }
}