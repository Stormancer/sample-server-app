using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    [Service(ServiceType = "tests.entry")]
    class TestController : ControllerBase
    {
        private readonly S2SProxy proxy;
        private readonly TestProxy selfProxy;
        private readonly IHost host;
        private readonly ISerializer serializer;
        private readonly IEnvironment environment;

        public TestController(S2SProxy proxy, TestProxy selfProxy, IHost host, ISerializer serializer, IEnvironment environment)
        {
            this.proxy = proxy;
            this.selfProxy = selfProxy;
            this.host = host;
            this.serializer = serializer;
            this.environment = environment;
        }



        [S2SApi]
        public Task<string> SameSceneS2SMethod(string msg)
        {
            return Task.FromResult(msg);

        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<string> TestSameSceneS2S(string msg, CancellationToken cancellationToken)
        {
            return selfProxy.SameSceneS2SMethod(msg, cancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public IAsyncEnumerable<TestDto> TestS2S(CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Range(0, TestPlugin.S2S_SCENE_COUNT).Merge(i => proxy.AsyncEnumerable(i.ToString(), cancellationToken), cancellationToken);
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
        public async Task<IEnumerable<int>> TestAppGlobalFunction(CancellationToken cancellationToken)
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
            return results.Select(t => t.Result);
        }
    }

    public static class AsyncEnumerableExtensions
    {
        public static IAsyncEnumerable<TResult> Merge<T, TResult>(this IAsyncEnumerable<T> source, Func<T, IAsyncEnumerable<TResult>> selector, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<TResult>();

            static async Task ReadAllAsync(ChannelWriter<TResult> writer, IAsyncEnumerable<T> sequence, Func<T, IAsyncEnumerable<TResult>> selector)
            {
                static async Task ReadImpl(IAsyncEnumerable<TResult> producer, ChannelWriter<TResult> writer)
                {
                    await foreach (var item in producer)
                    {
                        await writer.WriteAsync(item);
                    }
                }
                var list = await sequence.Select(selector).Select(producer => ReadImpl(producer, writer)).ToListAsync();
                await Task.WhenAll(list);
                writer.Complete();
            }
            _ = ReadAllAsync(channel.Writer, source, selector); //Don't wait for completion.

            return channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
