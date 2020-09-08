using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.Xray;

using System.Collections.Generic;

namespace Rhino.Connectors.Xray.UnitTests
{
    [TestClass]
    public class OnGoing
    {
        [TestMethod]
        public void TestMethod1()
        {
            var configu = new RhinoConfiguration
            {
                TestsRepository = new[]
                {
                    "XDP-39", "XDP-128"
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ProviderConfiguration = new RhinoProviderConfiguration
                {
                    Collection= "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "XDP",
                    Capabilities = new Dictionary<string, object>
                    {
                        ["bucketSize"] = 15
                    }
                },
                DriverParameters = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["driver"] = "ChromeDriver",
                        ["driverBinaries"] = @"D:\automation-env\web-drivers"
                    }
                },
                ScreenshotsConfiguration = new RhinoScreenshotsConfiguration
                {
                    KeepOriginal = true,
                    ReturnScreenshots = true
                },
                EngineConfiguration = new RhinoEngineConfiguration
                {
                    MaxParallel = 5
                }
            };
            var c = new XrayConnector(configu).Execute();
        }
    }
}
