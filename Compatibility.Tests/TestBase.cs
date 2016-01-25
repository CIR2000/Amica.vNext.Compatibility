using System;
using NUnit.Framework;

namespace Amica.vNext.Compatibility.Tests
{
    public abstract class TestBase
    {
        protected static HttpDataProvider HttpDataProvider;
		 
        [SetUp]
        public void Init()
        {

            HttpDataProvider = new HttpDataProvider()
            {
                ClientId = Environment.GetEnvironmentVariable("SentinelClientId"),
                Username = Environment.GetEnvironmentVariable("SentinelUsername"),
                Password = Environment.GetEnvironmentVariable("SentinelPassword"),
            };
        }

        [TearDown]
        public void Cleanup()
        {
            HttpDataProvider.Dispose();
        }

    }
}
