using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    public class TestDto
    {
        public string Value { get; set; }
        public bool Boolean { get; set; }
        public int Number { get; set; }
    }

    [Service(ServiceType = "tests.s2s", Named = true)]
    class S2SController : ControllerBase
    {
        [S2SApi]
        public async IAsyncEnumerable<TestDto> AsyncEnumerable()
        {
            foreach (var v in Enumerable.Range(0, 10))
            {
                yield return new TestDto { Number = v, Value = v.ToString() };
                await Task.Delay(100);
            }
        }
    }


    class TestServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if(ctx.ServiceType == "tests.s2s")
            {
                ctx.SceneId = TestPlugin.GetS2SSceneId(ctx.ServiceName);
            }

            return Task.CompletedTask;
        }
    }
}
