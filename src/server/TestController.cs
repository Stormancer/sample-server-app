using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    [Service(ServiceType = "tests.entry")]
    class TestController : ControllerBase
    {
        private readonly S2SProxy proxy;
        private readonly IHost host;
        private readonly ISerializer serializer;
        private readonly IEnvironment environment;

        public TestController(S2SProxy proxy, IHost host, ISerializer serializer, IEnvironment environment)
        {
            this.proxy = proxy;
            this.host = host;
            this.serializer = serializer;
            this.environment = environment;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task TestS2SAsyncEnumerable()
        {
            await foreach (var _ in proxy.AsyncEnumerable(CancellationToken.None))
            {

            }
        }

        /// <summary>
        /// Demonstrates disconnecting a player from the server.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task ServerForceDisconnect(Packet<IScenePeerClient> packet)
        {
            await packet.Connection.DisconnectFromServer("test");
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<int> TestAppGlobalFunction(CancellationToken cancellationToken)
        {
            

            using var rq = await host.StartAppFunctionRequest("scenes.count", cancellationToken);

            //Serialize the input (ie function arguments)
            await serializer.SerializeAsync(TestPlugin.S2S_SCENE_TEMPLATE, rq.Input, cancellationToken);
            //We inform the framework that we have finished writing. We could keep writing even after we started receiving responses.
            rq.Input.Complete();

            //Deserialize each result response as they arrive and produces a task that completes when all the hosts started sending their response.
            var results = await rq.Results.Select(result =>
            {
                var count = serializer.DeserializeAsync<int>(result.Output, cancellationToken);

                return count;
            }).ToListAsync();

            //We started receiving responses from all hosts, but they may still be sending data. Now we wait for all of them to finish sending responses.
            await Task.WhenAll(results);

            //Complete all output readers.
            await rq.Results.ForEachAsync(r => r.Output.Complete());

            //Aggregate the received results into a single scene count, and return the result to the client.
            return results.Sum(t => t.Result);
        }
    }
}
