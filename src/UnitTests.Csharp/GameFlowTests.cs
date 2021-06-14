using NUnit.Framework;
using NUnit.Framework.Internal;
using Stormancer;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class GameFlowTests
    {
        public const int ClientId1 = 0;
        public const int ClientId2 = 1;




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




        [Test]
        public async Task Authenticate()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 1; i++)
            {
                async Task auth_Impl(int clientId)
                {
                    var client = ClientFactory.GetClient(clientId);
                    var users = client.DependencyResolver.Resolve<UserApi>();
                    users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { } });
                    await users.Login();
                    if (users.State != GameConnectionState.Authenticated)
                    {
                        Assert.Fail("Authentication failed");
                    }

                    ClientFactory.ReleaseClient(clientId);
                }
                var id = ClientIdGenerator.CreateId();
                tasks.Add(Task.Run(() => auth_Impl(id)));
            }
            await Task.WhenAll(tasks);
        }



      
        [Test(Author = "JMD", Description = "Test direct IUserSessions.SendRequest ")]
        public async Task SendClientToClientUserRequest()
        {
            var testCts = new TaskCompletionSource<bool>();
            var client = ClientFactory.GetClient(ClientIdGenerator.CreateId());
            var users = client.DependencyResolver.Resolve<UserApi>();
            users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { } });
            await users.Login();
            users.SetOperationHandler("a", ctx => { testCts.SetResult(true); return Task.CompletedTask; });

            await users.SendRequestToUser<string>(users.UserId, "a", CancellationToken.None);

        }

        [Test(Author = "JMD", Description = "Test IUserSessions.SendRequest initiated by server.")]
        public async Task SendServerToClientUserRequest()
        {
            var testCts = new TaskCompletionSource<bool>();
            var client = ClientFactory.GetClient(ClientIdGenerator.CreateId());
            var users = client.DependencyResolver.Resolve<UserApi>();
            users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { } });
            await users.Login();
            users.SetOperationHandler("a", ctx => { testCts.SetResult(true); return Task.CompletedTask; });

            var testsScene = await client.ConnectToPublicScene("test-scene");

            await testsScene.RemoteAction("UsersTest.TestSendRequest");

        }

        [Test(Author = "JMD", Description = "Test  IUserSessions.SendRequest<T,U> initiated by server ")]
        public async Task SendServerToClientUserRequestGeneric()
        {
            var testCts = new TaskCompletionSource<bool>();
            var client = ClientFactory.GetClient(ClientIdGenerator.CreateId());
            var users = client.DependencyResolver.Resolve<UserApi>();
            users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { } });
            await users.Login();

            var data = "blabliblo";
            users.SetOperationHandler("b", ctx =>
            {

                ctx.RequestContext.SendValue(ctx.RequestContext.ReadObject<string>() == data);
                return Task.CompletedTask;
            });
            var testsScene = await client.ConnectToPublicScene("test-scene");

            Debug.Assert(await testsScene.RpcAsync<bool,string>("UsersTest.TestSendRequestGeneric", data));
            
        }

        [Test(Author = "JMD", Description = "Test  IUserSessions.SendRequest<T> initiated by server ")]
        public async Task SendServerToClientUserRequestGeneric2()
        {
            var testCts = new TaskCompletionSource<bool>();
            var client = ClientFactory.GetClient(ClientIdGenerator.CreateId());
            var users = client.DependencyResolver.Resolve<UserApi>();
            users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { } });
            await users.Login();

            var data = "b";
            var success = false;
            users.SetOperationHandler("c", ctx =>
            {
                var received = ctx.RequestContext.ReadObject<string>();
                success = received == data;
               
                return Task.CompletedTask;
            });
            var testsScene = await client.ConnectToPublicScene("test-scene");

            await testsScene.RemoteAction<string>("UsersTest.TestSendRequestGeneric2", data);
            Debug.Assert(success);

        }

        [Test]
        public async Task CreateParty()
        {
            async Task createParty_Impl(int clientId)
            {
                var client = ClientFactory.GetClient(clientId);

                var users = client.DependencyResolver.Resolve<UserApi>();
                users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { { "deviceidentifier", Guid.NewGuid().ToString() } } });
                await users.Login();

                var joinedEventReceived = 0;
                var party = client.DependencyResolver.Resolve<PartyApi>();
                party.OnPartyJoined += (c) =>
                {

                    joinedEventReceived++;
                };
                await party.CreateParty(new PartyRequestDto { GameFinderName = "testGameFinder" });

                await party.UpdatePlayerData("test");

                Assert.AreEqual(1, joinedEventReceived);

                ClientFactory.ReleaseClient(clientId);
            }

            var tasks = new List<Task>();
            for (int i = 0; i < 1; i++)
            {
                tasks.Add(createParty_Impl(ClientIdGenerator.CreateId()));
            }
            await Task.WhenAll(tasks);
        }
        [Test]
        public async Task FindGame()
        {
            async Task FindGame_Impl(int clientId)
            {
                var client = ClientFactory.GetClient(clientId);

                var users = client.DependencyResolver.Resolve<UserApi>();
                users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral", Parameters = new Dictionary<string, string> { { "deviceidentifier", Guid.NewGuid().ToString() } } });
                await users.Login();

                var party = client.DependencyResolver.Resolve<PartyApi>();

                await party.CreateParty(new PartyRequestDto { GameFinderName = "matchmaking" });

                var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
                var t = gameFinder.WhenGameFoundAsync(CancellationToken.None);

                await party.UpdatePlayerStatus(PartyUserStatus.Ready);


                await t;

                ClientFactory.ReleaseClient(clientId);

            }

            await Task.WhenAll(FindGame_Impl(ClientIdGenerator.CreateId()), FindGame_Impl(ClientIdGenerator.CreateId()));
        }



        //[Test]
        //public async Task ConnectToWrongGameFinder()
        //{
        //    var client = await GetAuthenticatedClient();
        //    try
        //    {
        //        var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
        //        await gameFinder.FindGame("matchmakerdefault", "json", "");
        //        Assert.Fail("Find game with the wrong parameter should throw an exception");
        //    }
        //    catch(Exception)
        //    {
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task ConnectToGameFinder()
        //{
        //    var client = await GetAuthenticatedClient();
        //    try
        //    {
        //        var validation = new TaskCompletionSource<bool>();
        //        var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
        //        gameFinder.OnGameFinderStateChanged = state =>
        //        {
        //            if (state.Status == GameFinderStatus.Searching)
        //            {
        //                validation.SetResult(true);
        //            }
        //        };
        //        var task = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        var tasks = new Task[2] { validation.Task, task };
        //        if (await Task.WhenAny(tasks) != validation.Task)
        //        {
        //            Assert.Fail("Wrong task finished first");
        //        }
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task ConnectToGameFinderAlreadyMatchmaking()
        //{
        //    var client = await GetAuthenticatedClient();
        //    try
        //    {
        //        var gameFinder = client.DependencyResolver.Resolve<GameFinder>();

        //        var request1 = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        var request2 = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        var tasks = new Task[2] { request1, request2 };
        //        var faultedTask = await Task.WhenAny(tasks);
        //        await faultedTask;
        //        Assert.Fail("This should have thrown an error as this is already in matchmaking");
        //    }
        //    catch (InvalidOperationException e)
        //    {
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task CancelGameFinder()
        //{
        //    var client = await GetAuthenticatedClient();
        //    try
        //    {
        //        var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
        //        var task = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        gameFinder.Cancel("matchmakerdefault");
        //        await task;
        //        Assert.Fail("This test should have thrown an operation cancelled exception");
        //    }
        //    catch(OperationCanceledException)
        //    {
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task RestartGameFinder()
        //{
        //    var client = await GetAuthenticatedClient();
        //    Task task;
        //    var gameFinder = client.DependencyResolver.Resolve<GameFinder>();

        //    try
        //    {
        //        task = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        gameFinder.Cancel("matchmakerdefault");
        //        await task;
        //    }
        //    catch(OperationCanceledException)
        //    {

        //    }
        //    try
        //    { 
        //        var validation = new TaskCompletionSource<bool>();
        //        gameFinder.OnGameFinderStateChanged = state =>
        //        {
        //            if (state.Status == GameFinderStatus.Searching)
        //            {
        //                validation.SetResult(true);
        //            }
        //        };
        //        task = gameFinder.FindGame("matchmakerdefault", "json", "{}");
        //        var tasks = new Task[2] { task, validation.Task };
        //        if(await Task.WhenAny(tasks) == validation.Task)
        //        {
        //            Assert.Pass();
        //        }
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task GameFound()
        //{
        //    var client = await GetAuthenticatedClient();
        //    var otherClient = await GetAuthenticatedClient();
        //    try
        //    {

        //        TaskCompletionSource<bool> player1Test = new TaskCompletionSource<bool>();
        //        TaskCompletionSource<bool> player2Test = new TaskCompletionSource<bool>();
        //        Task<bool>[] tasks = new Task<bool>[2] { player1Test.Task, player2Test.Task };
        //        var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
        //        gameFinder.OnGameFound += gameFound =>
        //        {
        //            player1Test.SetResult(true);
        //        };
        //        var otherGameFinder = otherClient.DependencyResolver.Resolve<GameFinder>();
        //        otherGameFinder.OnGameFound += gameFound =>
        //        {
        //            player2Test.SetResult(true);
        //        };
        //        await MatchClient(client, otherClient);
        //        await Task.WhenAll(tasks);
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client.Disconnect();
        //        otherClient.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task ConnectToGameSessionWithoutTunnel()
        //{
        //    var client1 = await GetAuthenticatedClient();
        //    var client2 = await GetAuthenticatedClient();
        //    try
        //    {
        //        TaskCompletionSource<bool> player1Test = new TaskCompletionSource<bool>();
        //        TaskCompletionSource<bool> player2Test = new TaskCompletionSource<bool>();
        //        Task<bool>[] tasks = new Task<bool>[2] { player1Test.Task, player2Test.Task };
        //        var gameFinder1 = client1.DependencyResolver.Resolve<GameFinder>();
        //        var gameFinder2 = client2.DependencyResolver.Resolve<GameFinder>();
        //        gameFinder1.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client1.DependencyResolver.Resolve<GameSession>();
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, false);
        //            player1Test.SetResult(true);
        //        };
        //        gameFinder2.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client2.DependencyResolver.Resolve<GameSession>();
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, false);
        //            player2Test.SetResult(true);
        //        };
        //        await MatchClient(client1, client2);
        //        await Task.WhenAll(tasks);
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client1.Disconnect();
        //        client2.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task ConnectToGameSessionWithTunnel()
        //{
        //    var client1 = await GetAuthenticatedClient();
        //    var client2 = await GetAuthenticatedClient();
        //    try
        //    {
        //        TaskCompletionSource<bool> validation = new TaskCompletionSource<bool>();
        //        var gameFinder1 = client1.DependencyResolver.Resolve<GameFinder>();
        //        var gameFinder2 = client2.DependencyResolver.Resolve<GameFinder>();
        //        gameFinder1.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client1.DependencyResolver.Resolve<GameSession>();
        //            gamesession.OnTunnelOpened = connectionParameters =>
        //            {
        //                validation.SetResult(true);
        //            };
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, true);
        //            await gamesession.EstablishDirectConnection();
        //        };
        //        gameFinder2.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client2.DependencyResolver.Resolve<GameSession>();
        //            gamesession.OnTunnelOpened = connectionParameters =>
        //            {
        //                validation.SetResult(true);
        //            };
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, true);
        //            await gamesession.EstablishDirectConnection();
        //        };
        //        await MatchClient(client1, client2);
        //        await validation.Task;
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client1.Disconnect();
        //        client2.Disconnect();
        //    }
        //}

        //[Test]
        //public async Task GameSessionMessage()
        //{
        //    var client1 = await GetAuthenticatedClient();
        //    var client2 = await GetAuthenticatedClient();
        //    try
        //    {
        //        TaskCompletionSource<bool> Message1Tcs = new TaskCompletionSource<bool>();
        //        TaskCompletionSource<bool> Message2Tcs = new TaskCompletionSource<bool>();
        //        var tasks = new Task<bool>[2] { Message1Tcs.Task, Message2Tcs.Task };
        //        var gameFinder1 = client1.DependencyResolver.Resolve<GameFinder>();
        //        var gameFinder2 = client2.DependencyResolver.Resolve<GameFinder>();
        //        gameFinder1.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client1.DependencyResolver.Resolve<GameSession>();
        //            gamesession.OnPeerConnected = scenePeer =>
        //            {
        //                scenePeer.Send("test", stream => { }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
        //            };
        //            gamesession.OnConnectingToScene = scene =>
        //            {
        //                scene.AddRoute("test", packet =>
        //                {
        //                    Message1Tcs.SetResult(true);
        //                }, MessageOriginFilter.Peer);
        //            };
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, false);
        //            await gamesession.EstablishDirectConnection();
        //        };
        //        gameFinder2.OnGameFound = async gameFoundEvent =>
        //        {
        //            var gamesession = client2.DependencyResolver.Resolve<GameSession>();
        //            gamesession.OnPeerConnected = scenePeer =>
        //            {
        //                scenePeer.Send("test", stream => { }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
        //            };
        //            gamesession.OnConnectingToScene = scene =>
        //            {
        //                scene.AddRoute("test", packet =>
        //                {
        //                    Message2Tcs.SetResult(true);
        //                }, MessageOriginFilter.Peer);
        //            };
        //            await gamesession.ConnectToGameSession(gameFoundEvent.Data.ConnectionToken, false);
        //            await gamesession.EstablishDirectConnection();
        //        };
        //        await MatchClient(client1, client2);
        //        Task.WaitAll(tasks);
        //        Assert.Pass();
        //    }
        //    finally
        //    {
        //        client1.Disconnect();
        //        client2.Disconnect();
        //    }
        //}
    }
}
