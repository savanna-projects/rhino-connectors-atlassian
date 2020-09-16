using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api.Contracts.Configuration;

using System.Collections.Generic;
using System.Linq;

namespace Rhino.Connectors.Xray.UnitTests
{
    [TestClass]
    public class OnGoing
    {
        [TestMethod]
        public void DemoConfiguration()
        {
            var configu = new RhinoConfiguration
            {
                TestsRepository = new[]
                {
                    "XDP-39"
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ProviderConfiguration = new RhinoProviderConfiguration
                {
                    Collection = "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "XDP",
                    BugManager = true,
                    Capabilities = new Dictionary<string, object>
                    {
                        ["bucketSize"] = 15,
                        //["dryRun"] = true
                    }
                },
                DriverParameters = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["driver"] = "ChromeDriver",
                        ["driverBinaries"] = @"D:\automation-env\web-drivers",
                        ["capabilities"] = new Dictionary<string, object>
                        {
                            ["build"] = "Test Build",
                            ["project"] = "Bug Manager"
                        },
                        ["options"] = new Dictionary<string, object>
                        {
                            ["arguments"] = new[]
                            {
                                "--ignore-certificate-errors",
                                "--disable-popup-blocking",
                                "--incognito"
                            }
                        }
                    }
                },
                ScreenshotsConfiguration = new RhinoScreenshotsConfiguration
                {
                    KeepOriginal = true,
                    ReturnScreenshots = true
                },
                EngineConfiguration = new RhinoEngineConfiguration
                {
                    MaxParallel = 5,
                    Priority = 5,
                }
            };
            new XrayConnector(configu).Execute();
        }

        //[TestMethod]
        public void DemoCreateTestCase()
        {
            var configu = new RhinoConfiguration
            {
                TestsRepository = new[]
                {
                    "XDP-240"
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ProviderConfiguration = new RhinoProviderConfiguration
                {
                    Collection = "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "XDP",
                    BugManager = true,
                    Capabilities = new Dictionary<string, object>
                    {
                        ["bucketSize"] = 15,
                        ["testPlans"] = new[] { "XDP-39", "XDP-128" }
                        //["dryRun"] = true
                    }
                },
                DriverParameters = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["driver"] = "ChromeDriver",
                        ["driverBinaries"] = @"D:\automation-env\web-drivers",
                        ["capabilities"] = new Dictionary<string, object>
                        {
                            ["build"] = "Test Build",
                            ["project"] = "Bug Manager"
                        }
                    }
                },
                ScreenshotsConfiguration = new RhinoScreenshotsConfiguration
                {
                    KeepOriginal = true,
                    ReturnScreenshots = true
                },
                EngineConfiguration = new RhinoEngineConfiguration
                {
                    MaxParallel = 5,
                    Priority = 5
                }
            };
            var connector = new XrayConnector(configu);
            var testCase = connector.ProviderManager.TestRun.TestCases.ElementAt(0);
            testCase.Scenario = "Created by CreateTestCase";

            connector.ProviderManager.CreateTestCase(testCase);
        }
    }
}