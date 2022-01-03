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
            Assert.IsTrue(true);
        }
    }
}
