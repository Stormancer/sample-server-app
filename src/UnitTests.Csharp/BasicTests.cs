using NUnit.Framework;
using Stormancer;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    class BasicTests
    {

        [SetUp]
        public void Setup()
        {
            ClientFactory.SetConfigFactory(() =>
            {

                var config = ClientConfiguration.Create(Config.ServerEndpoint, Config.Account, Config.Application);
                config.Plugins.Add(new AuthenticationPlugin());
                config.Plugins.Add(new GameSessionPlugin());
                config.Plugins.Add(new PartyPlugin());
                config.Plugins.Add(new GameFinderPlugin());
                config.Logger = ConsoleLogger.Instance;
                return config;
            });


        }

        /// <summary>
        /// Starts with _ to make sure it's the first test executed.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task _Connect()
        {
            var client = ClientFactory.GetClient(0);

            var testsScene = await client.ConnectToPublicScene("test-scene");
        }
        [Test]
        public async Task SceneNotFound()
        {
            var client = ClientFactory.GetClient(0);

            try
            {
                var testsScene = await client.ConnectToPublicScene("missing-scene");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("Can't get the scene endpoint response: Failed to get token for scene"));
            }

        }

        [Test]
        public async Task RouteNotFound()
        {
            try
            {
                var client = ClientFactory.GetClient(0);

                var testsScene = await client.ConnectToPublicScene("test-scene");
                testsScene.Send("missing-route", s => { });
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.StartsWith("The scene peer does not contains a route named missing-route"));
            }
        }

        [Test]
        public async Task ServerForceDisconnect()
        {
            var client = ClientFactory.GetClient(0);

            var testsScene = await client.ConnectToPublicScene("test-scene");



            var tcs = new TaskCompletionSource<bool>();
            testsScene.SceneConnectionStateObservable.Subscribe(ctx => { if (ctx.State == Stormancer.Core.ConnectionState.Disconnected && ctx.Reason == "test") tcs.SetResult(true); });
            testsScene.Send("Test.ServerForceDisconnect", "test");

            await await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, new CancellationTokenSource(400).Token));

        }

        [Test]
        public async Task RunAppGlobalFunction()
        {
            var client = ClientFactory.GetClient(0);

            var testsScene = await client.ConnectToPublicScene("test-scene");

            var tests = await testsScene.RpcAsync<IEnumerable<int>>("Test.TestAppGlobalFunction");

        }

        [Test]
        public async Task S2SSameScene()
        {
            var client = ClientFactory.GetClient(0);

            var testsScene = await client.ConnectToPublicScene("test-scene");

            await testsScene.RemoteAction("Test.TestSameSceneS2S", CancellationToken.None);
        }

        [Test]
        public async Task S2S()
        {
            var client = ClientFactory.GetClient(0);

            var testsScene = await client.ConnectToPublicScene("test-scene");

            await testsScene.RemoteAction("Test.TestS2S", CancellationToken.None);
        }
    }

}
