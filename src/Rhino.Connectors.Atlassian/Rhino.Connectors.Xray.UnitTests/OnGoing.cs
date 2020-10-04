using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api;
using Rhino.Api.Contracts.AutomationProvider;
using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.Xray.Cloud;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                Name = "For Integration Testing",
                TestsRepository = new[]
                {
                    "RHIN-1"/*"XT-7"*//*, "XT-8", "XT-9"*//*, "XT-1", "XT-6"*/
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ConnectorConfiguration = new RhinoConnectorConfiguration
                {
                    Collection = "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "RHIN",
                    //Collection = "https://pangobugs.atlassian.net",
                    //Password = "aLNwnhE8fupLguQ6fwYo8A00",
                    //User = "s_roei@msn.com",
                    //Project = "XT",
                    BugManager = true
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
                    MaxParallel = 1
                },
                Capabilities = new Dictionary<string, object>
                {
                    [$"{Connector.JiraXRay}:options"] = new Dictionary<string, object>
                    {
                        ["bucketSize"] = 15,
                        ["dryRun"] = false,
                        //[AtlassianCapabilities.TestType] = "Xray Test",
                        //[AtlassianCapabilities.PreconditionsType] = "Precondition"
                    }
                }
            };
            var connector = new XrayConnector(configu);
            connector.Execute();
            //var testCases = connector.ProviderManager.GetTestCases("XT-7").First();
            //connector.ProviderManager.CreateTestCase(connector.ProviderManager.TestRun.TestCases.First());
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
                ConnectorConfiguration = new RhinoConnectorConfiguration
                {
                    Collection = "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "XDP",
                    BugManager = true,
                },
                Capabilities = new Dictionary<string, object>
                {
                    ["bucketSize"] = 15,
                    ["testPlans"] = new[] { "XDP-39", "XDP-128" }
                    //["dryRun"] = true
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

        [TestMethod]
        public void DemoConfiguration2()
        {
            var configu = new RhinoConfiguration
            {
                Name = "For Integration Testing",
                TestsRepository = new[]
                {
                    "RA-1"/*"XT-7"*//*, "XT-8", "XT-9"*//*, "XT-1", "XT-6"*/
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ConnectorConfiguration = new RhinoConnectorConfiguration
                {
                    Collection = "http://localhost:8080",
                    Password = "admin",
                    User = "admin",
                    Project = "RA",
                    //Collection = "https://pangobugs.atlassian.net",
                    //Password = "aLNwnhE8fupLguQ6fwYo8A00",
                    //User = "s_roei@msn.com",
                    //Project = "XT",
                    BugManager = true,
                    Connector = Connector.JiraXRay
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
                    MaxParallel = 8
                },
                Capabilities = new Dictionary<string, object>
                {
                    [$"{Connector.JiraXRay}:options"] = new Dictionary<string, object>
                    {
                        ["dryRun"] = false,
                        ["bucketSize"] = 15,
                        //[AtlassianCapabilities.TestType] = "Xray Test",
                        //[AtlassianCapabilities.PreconditionsType] = "Precondition"
                    }
                }
            };
            //var a = new RhinoTestCase() { Scenario = "Test Test Test" };
            var connector = new XrayConnector(configu);
            //connector.ProviderManager.CreateTestCase(a);
            connector.Execute();
            //var testCases = connector.ProviderManager.GetTestCases("XT-7").First();
            //connector.ProviderManager.CreateTestCase(connector.ProviderManager.TestRun.TestCases.First());
        }

        [TestMethod]
        public void DemoConfiguration3()
        {
            var configu = new RhinoConfiguration
            {
                Name = "For Integration Testing",
                TestsRepository = new[]
                {
                    "RA-1340"/*"RHIN-1"*//*"XT-58"*//*, "XT-8", "XT-9"*//*, "XT-1", "XT-6"*/
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ConnectorConfiguration = new RhinoConnectorConfiguration
                {
                    //Collection = "http://localhost:8080",
                    //Password = "admin",
                    //User = "admin",
                    //Project = "RHIN",

                    //Collection = "https://pangobugs.atlassian.net",
                    //Password = "aLNwnhE8fupLguQ6fwYo8A00",
                    //User = "s_roei@msn.com",
                    //Project = "XT",
                    //BugManager = true,
                    //Connector = Connector.JiraXryCloud,

                    Collection = "https://rhinoapi.atlassian.net",
                    Password = "0hshf1gBkfZqsoABp9oO173D",
                    User = "rhino.api@gmail.com",
                    Project = "RA",
                    BugManager = true,
                    Connector = Connector.JiraXryCloud
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
                    MaxParallel = 5
                },
                Capabilities = new Dictionary<string, object>
                {
                    [$"{Connector.JiraXryCloud}:options"] = new Dictionary<string, object>
                    {
                        ["dryRun"] = false,
                        ["bucketSize"] = 40,
                        //[AtlassianCapabilities.TestType] = "Xray Test",
                        //[AtlassianCapabilities.PreconditionsType] = "Precondition"
                    }
                }
            };
            //var a = new RhinoTestCase() { Scenario = "Test Test Test" };
            var connector = new XrayCloudConnector(configu);
            //connector.ProviderManager.CreateTestCase(a);
            connector.Execute();
            //var testCases = connector.ProviderManager.GetTestCases("XT-7").First();
            //connector.ProviderManager.CreateTestCase(connector.ProviderManager.TestRun.TestCases.First());
        }

        [TestMethod]
        public void T()
        {

            FileInfo fi = new FileInfo(@"D:\garbage\rhino-issue-1.txt");
            byte[] fileContents = File.ReadAllBytes(fi.FullName);
            var urlPath = "http://localhost:8080/rest/api/2/issue/" + "RHIN-21" + "/attachments";
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;
            requestMessage.Headers.Authorization = GetAuthenticationHeader("admin", "admin");
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            var multiPartContent = new MultipartFormDataContent("----MyGreatBoundary");
            var byteArrayContent = new ByteArrayContent(fileContents);
            byteArrayContent.Headers.Add("Content-Type", "image/png");
            byteArrayContent.Headers.Add("X-Atlassian-Token", "no-check");
            multiPartContent.Add(byteArrayContent, "file", fi.Name);
            requestMessage.Content = multiPartContent;

            HttpClient httpClient = new HttpClient();
            try
            {
                Task<HttpResponseMessage> httpRequest = httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                HttpResponseMessage httpResponse = httpRequest.Result;
                HttpStatusCode statusCode = httpResponse.StatusCode;
                HttpContent responseContent = httpResponse.Content;

                if (responseContent != null)
                {
                    Task<String> stringContentsTask = responseContent.ReadAsStringAsync();
                    String stringContents = stringContentsTask.Result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static AuthenticationHeaderValue GetAuthenticationHeader(string user, string password)
        {
            // setup: provider authentication and base address
            var header = $"{user}:{password}";
            var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

            // header
            return new AuthenticationHeaderValue("Basic", encodedHeader);
        }
    }
}