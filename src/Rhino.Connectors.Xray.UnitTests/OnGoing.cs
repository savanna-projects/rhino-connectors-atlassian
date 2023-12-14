#pragma warning disable
using Gravity.Services.DataContracts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api.Contracts.Configuration;
using Rhino.Api.Extensions;
using Rhino.Connectors.AtlassianClients.Contracts;
using Rhino.Connectors.AtlassianClients.Extensions;
using Rhino.Connectors.AtlassianClients.Framework;
using Rhino.Connectors.Xray.Cloud;
using Rhino.Connectors.Xray.Cloud.Text;
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
        //[TestMethod]
        public void Test()
        {
            var json = File.ReadAllText(@"E:\Garbage\aaaa.txt");
            var configuration = JsonSerializer.Deserialize<RhinoConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var connector = new XrayCloudConnector(configuration);
            connector.Invoke();

            //configuration.Invoke(Utilities.Types);
        }
    }
}
