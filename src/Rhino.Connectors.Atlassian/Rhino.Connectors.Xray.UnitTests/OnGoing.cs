using Microsoft.VisualStudio.TestTools.UnitTesting;

using Rhino.Api.Contracts.Configuration;
using Rhino.Connectors.Xray;


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
                    "XDP-40"
                }
            };
            var c = new XrayConnector(configu);
        }
    }
}
