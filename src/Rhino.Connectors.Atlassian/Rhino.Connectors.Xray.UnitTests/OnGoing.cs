#pragma warning disable
using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                    UserName = "admin",
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
                    UserName = "admin",
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
                    UserName = "admin",
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
                    "RHIN-40"/*"RHIN-1"*//*"XT-58"*//*, "XT-8", "XT-9"*//*, "XT-1", "XT-6"*/
                },
                Authentication = new Authentication
                {
                    UserName = "automation@rhino.api",
                    Password = "Aa123456!"
                },
                ConnectorConfiguration = new RhinoConnectorConfiguration
                {
                    //Collection = "http://localhost:8081",
                    //Password = "admin",
                    //UserName = "admin",
                    //Project = "RHIN",
                    //Connector = Connector.JiraXRay,
                    //BugManager = true

                    //Collection = "https://pangobugs.atlassian.net",
                    //Password = "fTxBAqDoGTQsGlDAUjqdCF39",
                    //UserName = "yair@pango.co.il",
                    //Project = "PNG",
                    //BugManager = true,
                    //Connector = Connector.JiraXryCloud,

                    Collection = "https://rhinoapi.atlassian.net",
                    Password = "0hshf1gBkfZqsoABp9oO173D",
                    UserName = "rhino.api@gmail.com",
                    Project = "RA",
                    BugManager = true,
                    Connector = Connector.JiraXryCloud
                },
                DriverParameters = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["driver"] = "ChromeDriver",
                        ["driverBinaries"] = @"D:\automation_env\web_drivers",
                        ["capabilities"] = new Dictionary<string, object>
                        {
                            //["build"] = "Test Build",
                            //["project"] = "Bug Manager"
                        },
                        ["options"] = new Dictionary<string, object>
                        {
                            //["arguments"] = new[]
                            //{
                            //    "--ignore-certificate-errors",
                            //    "--disable-popup-blocking",
                            //    "--incognito"
                            //}
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
            var connector = new XrayConnector(configu);
            //connector.ProviderManager.CreateTestCase(a);
            connector.Execute();
            //var testCases = connector.ProviderManager.GetTestCases("XT-7").First();
            //connector.ProviderManager.CreateTestCase(connector.ProviderManager.TestRun.TestCases.First());
        }

        [TestMethod]
        public void DemoConfiguration4()
        {
            // get configuration
            var requestBody = File.ReadAllText(@"D:\garbage\pp.txt");
            var configuration = JsonConvert.DeserializeObject<RhinoConfiguration>(requestBody).ApplySettings();

            // get provider capabilities
            var jsonObject = JObject.Parse(requestBody);
            var capabilities = jsonObject.SelectToken("capabilities");
            var options = jsonObject.SelectToken($"capabilities.{configuration.ConnectorConfiguration.Connector}:options");

            var onOptions = options != null
                ? JsonConvert.DeserializeObject<IDictionary<string, object>>($"{options}")
                : new Dictionary<string, object>();

            configuration.Capabilities = capabilities != null
                ? JsonConvert.DeserializeObject<IDictionary<string, object>>($"{capabilities}")
                : new Dictionary<string, object>();

            configuration.Capabilities[$"{configuration.ConnectorConfiguration.Connector}:options"] = onOptions;

            // execute
            var r = new XrayConnector(configuration).Execute();
            //var responseBody = configuration.Execute(Utilities.Types);

        }

        [TestMethod]
        public void T()
        {
            // setup
            //var baseAddress = GetBaseAddress(authentication);
            //var apiVersion = authentication.GetCapability(AtlassianCapabilities.JiraApiVersion, "latest");
            //var urlPath = $"{baseAddress}/rest/api/{apiVersion}/issue/{idOrKey}/attachments";
            var urlPath = "https://xray.cloud.xpand-it.com/api/internal/attachments?testRunId=5f79f0fbb301db0019621ea6";

            // build request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;
            requestMessage.Headers.Authorization = GetAuthenticationHeader("rhino.api@gmail.com", "0hshf1gBkfZqsoABp9oO173D");
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            // build multi part content
            var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

            // build file content
            var files = new[]
            {
                (Path: @"D:\garbage\1.png", "application/octet-stream")
            };
            foreach (var (Path, ContentType) in files)
            {
                var fileInfo = new FileInfo(Path);
                var fileContents = File.ReadAllBytes(fileInfo.FullName);
                var byteArrayContent = new ByteArrayContent(fileContents);
                byteArrayContent.Headers.Add("Content-Type", ContentType);
                //byteArrayContent.Headers.Add("X-Atlassian-Token", "no-check");
                multiPartContent.Add(byteArrayContent, "attachment", fileInfo.Name);
            }

            // set request content
            requestMessage.Content = multiPartContent;
            requestMessage = FactorXpandMessage(requestMessage, new HttpCommand
            {
                Headers = new Dictionary<string, string>
                {
                    ["X-acpt"] = "RA-1359"
                }
            });

            using (var client = new HttpClient())
            {
                var r = client.SendAsync(requestMessage).GetAwaiter().GetResult();
            }

            // post response to /api/internal/testrun/5f79f0fbb301db0019621ea6/step/31f75693-636f-40b9-8dfe-6ff1311efa41/evidence

            //FileInfo fi = new FileInfo(@"D:\garbage\rhino-issue-1.txt");
            //byte[] fileContents = File.ReadAllBytes(fi.FullName);
            //var urlPath = "http://localhost:8080/rest/api/2/issue/" + "RHIN-21" + "/attachments";
            //var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            //requestMessage.Headers.ExpectContinue = false;
            //requestMessage.Headers.Authorization = GetAuthenticationHeader("admin", "admin");
            //requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            //var multiPartContent = new MultipartFormDataContent("----MyGreatBoundary");
            //var byteArrayContent = new ByteArrayContent(fileContents);
            //byteArrayContent.Headers.Add("Content-Type", "image/png");
            //byteArrayContent.Headers.Add("X-Atlassian-Token", "no-check");
            //multiPartContent.Add(byteArrayContent, "file", fi.Name);
            //requestMessage.Content = multiPartContent;

            //HttpClient httpClient = new HttpClient();
            //try
            //{
            //    Task<HttpResponseMessage> httpRequest = httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
            //    HttpResponseMessage httpResponse = httpRequest.Result;
            //    HttpStatusCode statusCode = httpResponse.StatusCode;
            //    HttpContent responseContent = httpResponse.Content;

            //    if (responseContent != null)
            //    {
            //        Task<String> stringContentsTask = responseContent.ReadAsStringAsync();
            //        String stringContents = stringContentsTask.Result;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //}
        }

        private HttpRequestMessage FactorXpandMessage(HttpRequestMessage message, HttpCommand command)
        {
            var j = new JiraAuthentication
            {
                UserName = "rhino.api@gmail.com",
                Password = "0hshf1gBkfZqsoABp9oO173D",
                Collection = "https://rhinoapi.atlassian.net"
            };
            // constants
            const string Xacpt = "X-acpt";

            // extract
            var response = JiraCommandsRepository.GetToken("RA", command.Headers[Xacpt]).Send(j).AsJToken();
            var options = $"{response.SelectTokens("..options").FirstOrDefault()}";
            var token = $"{JToken.Parse(options).SelectToken("contextJwt")}";

            // apply
            message.Headers.Add(Xacpt, token);
            //message.RequestUri = new Uri($"https://xray.cloud.xpand-it.com{command.Route}");

            // get
            return message;
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

    public static class Ext
    {
        public static RhinoConfiguration ApplySettings(this RhinoConfiguration configuration)
        {
            // reporting
            configuration.ReportConfiguration.ReportOut = Environment.CurrentDirectory;

            configuration.ReportConfiguration.LogsOut = Environment.CurrentDirectory;

            configuration.ReportConfiguration.Archive = false;

            configuration.ReportConfiguration.Reporters = new[] { "reporterBasic" };

            //configuration.ReportConfiguration..ConnectionString =
            //    appSettings.GetValue<string>("rhino:reportConfiguration:connectionString");

            // screenshots
            configuration.ScreenshotsConfiguration.ScreenshotsOut = Environment.CurrentDirectory;

            // updated state
            return configuration;
        }
    }
}