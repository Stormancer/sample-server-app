using NUnit.Framework;
using Stormancer;
using Stormancer.Plugins;
using Stormancer.Replication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class ReplicationTests
    {
        [SetUp]
        public void Setup()
        {
            ClientFactory.SetConfigFactory(() =>
            {

                var config = ClientConfiguration.Create(Config.ServerEndpoint, Config.Account, Config.Application);
                config.Plugins.Add(new AuthenticationPlugin());
                config.Plugins.Add(new PartyPlugin());
                config.Plugins.Add(new GameFinderPlugin());
                config.Plugins.Add(new ReplicationPlugin());
                config.Logger = ConsoleLogger.Instance;
                return config;
            });

        }

        async Task<Scene> ConnectToGameSession_Impl(Client client, Action<Scene> gameSessionInitializer)
        {


            var users = client.DependencyResolver.Resolve<UserApi>();
            users.OnGetAuthParameters = () => Task.FromResult(new AuthParameters { Type = "ephemeral" });
            await users.Login();

            var party = client.DependencyResolver.Resolve<PartyApi>();

            await party.CreateParty(new PartyRequestDto { GameFinderName = "matchmaking" });

            var gameFinder = client.DependencyResolver.Resolve<GameFinder>();
            var t = gameFinder.WhenGameFoundAsync(CancellationToken.None);

            await party.UpdatePlayerStatus(PartyUserStatus.Ready);


            var ev = await t;
            return await client.ConnectToPrivateScene(ev.Data.ConnectionToken, gameSessionInitializer);
        }


        [Test]
        public async Task ReplicateEntity()
        {
            var nbEvents = 0;
            var watch = new Stopwatch();
            var names = new List<string> { "Félinë", "Talrae"/*, "Nari", "Shyn'tae"*/ };
            async Task ReplicateEntity_Impl(int clientId, string myName)
            {
                var client = ClientFactory.GetClient(clientId);
                var scene = await ConnectToGameSession_Impl(client, s =>
                {

                    s.DependencyResolver.Resolve<ReplicationApi>().Configure(b => b
                        .ConfigureEntityBuilder(ctx =>
                        {
                            switch (ctx.Entity.Type)
                            {
                                case "user":
                                    ctx.Entity.AddComponent<UserState>("userState");
                                    break;
                                default:
                                    throw new NotSupportedException($"Entity type '{ctx.Entity.Type}' not supported.");
                            }
                            return Task.CompletedTask;// Task.Delay(1000);
                        })
                        .ConfigureViewDataPolicy("authority", b => b)
                    );


                });
                watch.Start();
                var rep = scene.DependencyResolver.Resolve<ReplicationApi>();

                var tcs = new TaskCompletionSource<bool>();
                var result = new List<Entity>();

                rep.EntitiesChanged += (EntitiesChangedEventArgs batch) =>
                {
                    nbEvents++;
                    var success = true;
                    Debug.WriteLine($"Event : [{myName}] {string.Join(", ", batch.Changes.Select(c => $"{c.ChangeType}:{c.Entity.Id}"))}");

                    foreach (var name in names)
                    {
                        result.Clear();
                        rep.Entities.Query((Entity e, object userState) => e.GetComponent<UserState>("userState").Name == (string)userState, result, name);
                        if (!result.Any())
                        {
                            success = false;
                            break;
                        }
                    }
                    if (success)
                    {
                        watch.Stop();
                        System.Diagnostics.Debug.WriteLine($"synchronized: {watch.ElapsedMilliseconds}ms");


                        tcs.TrySetResult(true);
                    }


                };

                await rep.WhenAuthoritySynchronized();
                //Create an entity.
                watch.Stop();
                System.Diagnostics.Debug.WriteLine($"Init: {watch.ElapsedMilliseconds}ms");

                watch.Restart();

                await rep.CreateEntity("user", e =>
                {
                    e.GetComponent<UserState>("userState").Name = myName;
                });

                var cts = new CancellationTokenSource(30000000);

                await await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));


                var results = new List<Entity>();
                rep.Entities.Query((_, _) => true, results, null);
                ClientFactory.ReleaseClient(clientId);
            }

            await Task.WhenAll(names.Select(name => ReplicateEntity_Impl(ClientIdGenerator.CreateId(), name)));
            Debug.Assert(nbEvents == names.Count * names.Count);
        }

        [Test]
        public async Task AutoRefreshReplication()
        {
            var nbEvents = 0;
            var watch = new Stopwatch();
            var names = new List<string> { "Félinë", "Talrae"/*, "Nari", "Shyn'tae"*/ };
            async Task ReplicateEntity_Impl(int clientId, string myName)
            {
                var client = ClientFactory.GetClient(clientId);
                var scene = await ConnectToGameSession_Impl(client, s =>
                {

                    s.DependencyResolver.Resolve<ReplicationApi>().Configure(b => b
                        .ConfigureEntityBuilder(ctx =>
                        {
                            switch (ctx.Entity.Type)
                            {
                                case "user":
                                    ctx.Entity.AddComponent<UserState>("userState");
                                    ctx.Entity.AddComponent<TimerState>("timer");
                                    break;
                                default:
                                    throw new NotSupportedException($"Entity type '{ctx.Entity.Type}' not supported.");
                            }
                            return Task.Delay(1000);
                        })
                        .ConfigureViewDataPolicy("authority", b => b)
                    );


                });
                watch.Start();
                var rep = scene.DependencyResolver.Resolve<ReplicationApi>();

                var tcs = new TaskCompletionSource<bool>();
                var result = new List<Entity>();

                rep.EntitiesChanged += (EntitiesChangedEventArgs batch) =>
                {
                    nbEvents++;
                    var success = true;
                    Debug.WriteLine($"Event : [{myName}] {string.Join(", ", batch.Changes.Select(c => $"{c.ChangeType}:{c.Entity.Id}"))}");

                    foreach (var name in names)
                    {
                        result.Clear();
                        rep.Entities.Query((Entity e, object userState) => e.GetComponent<UserState>("userState").Name == (string)userState, result, name);
                        if (!result.Any())
                        {
                            success = false;
                            break;
                        }
                    }
                    if (success)
                    {
                        watch.Stop();
                        System.Diagnostics.Debug.WriteLine($"synchronized: {watch.ElapsedMilliseconds}ms");


                        tcs.TrySetResult(true);
                    }


                };

                await rep.WhenAuthoritySynchronized();
                //Create an entity.
                watch.Stop();
                System.Diagnostics.Debug.WriteLine($"Init: {watch.ElapsedMilliseconds}ms");

                watch.Restart();

                await rep.CreateEntity("user", e =>
                {
                    e.GetComponent<UserState>("userState").Name = myName;
                });

                var cts = new CancellationTokenSource(30000000);

                await await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));


                var results = new List<Entity>();
                rep.Entities.Query((_, _) => true, results, null);
                ClientFactory.ReleaseClient(clientId);
            }

            await Task.WhenAll(names.Select(name => ReplicateEntity_Impl(ClientIdGenerator.CreateId(), name)));
            //Debug.Assert(nbEvents == names.Count * names.Count);
        }

        [Test]
        public async Task SendMessageToAuthority()
        {
            var tcs = new TaskCompletionSource<bool>();
            var nbEvents = 0;
            var watch = new Stopwatch();
            var names = new List<string> { "Félinë", "Talrae"/*, "Nari", "Shyn'tae"*/ };
            async Task ReplicateEntity_Impl(int clientId, string myName)
            {
                var client = ClientFactory.GetClient(clientId);
                var scene = await ConnectToGameSession_Impl(client, s =>
                {

                    s.DependencyResolver.Resolve<ReplicationApi>().Configure(b => b
                        .ConfigureEntityBuilder(ctx =>
                        {
                            switch (ctx.Entity.Type)
                            {
                                case "user":
                                    ctx.Entity.AddComponent<UserState>("userState");
                                    ctx.Entity.AddComponent<TimerState>("timer");
                                    ctx.Entity.ConfigureMessageHandler("test", m => tcs.TrySetResult(true));
                                    break;
                                default:
                                    throw new NotSupportedException($"Entity type '{ctx.Entity.Type}' not supported.");
                            }
                            return Task.Delay(1000);
                        })
                        .ConfigureViewDataPolicy("authority", b => b)
                    );


                });
                watch.Start();
                var rep = scene.DependencyResolver.Resolve<ReplicationApi>();

              
                var result = new List<Entity>();

                rep.EntitiesChanged += (EntitiesChangedEventArgs batch) =>
                {
                    nbEvents++;
                    var success = true;
                    Debug.WriteLine($"Event : [{myName}] {string.Join(", ", batch.Changes.Select(c => $"{c.ChangeType}:{c.Entity.Id}"))}");

                    foreach (var name in names)
                    {
                        result.Clear();
                        rep.Entities.Query((Entity e, object userState) => e.GetComponent<UserState>("userState").Name == (string)userState, result, name);
                        if (!result.Any())
                        {
                            success = false;
                            break;
                        }
                    }
                    if (success)
                    {
                        watch.Stop();
                        System.Diagnostics.Debug.WriteLine($"synchronized: {watch.ElapsedMilliseconds}ms");

                        foreach(var change in batch.Changes.Where(arg => !arg.Entity.IsAuthority))
                        {
                            change.Entity.SendMessageToAuthority("test", m => m);
                        }
                        
                    }


                };

                await rep.WhenAuthoritySynchronized();
                //Create an entity.
                watch.Stop();
                System.Diagnostics.Debug.WriteLine($"Init: {watch.ElapsedMilliseconds}ms");

                watch.Restart();

                await rep.CreateEntity("user", e =>
                {
                    e.GetComponent<UserState>("userState").Name = myName;
                });

                var cts = new CancellationTokenSource(30000000);

                await await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));


                var results = new List<Entity>();
                rep.Entities.Query((_, _) => true, results, null);
                ClientFactory.ReleaseClient(clientId);
            }

            await Task.WhenAll(names.Select(name => ReplicateEntity_Impl(ClientIdGenerator.CreateId(), name)));
            //Debug.Assert(nbEvents == names.Count * names.Count);
        }


        [Test]
        public async Task BroadcastMessage()
        {
            var tcs = new TaskCompletionSource();
            var msgtcs = new TaskCompletionSource();
            var nbEvents = 0;
            var watch = new Stopwatch();
            var names = new List<string> { "Félinë", "Talrae"/*, "Nari", "Shyn'tae"*/ };
            async Task ReplicateEntity_Impl(int clientId, string myName)
            {
                var client = ClientFactory.GetClient(clientId);
                var scene = await ConnectToGameSession_Impl(client, s =>
                {

                    s.DependencyResolver.Resolve<ReplicationApi>().Configure(b => b
                        .ConfigureEntityBuilder(ctx =>
                        {
                            switch (ctx.Entity.Type)
                            {
                                case "user":
                                    ctx.Entity.AddComponent<UserState>("userState");
                                    ctx.Entity.AddComponent<TimerState>("timer");
                                    ctx.Entity.ConfigureMessageHandler("test", m => msgtcs.TrySetResult());
                                    break;
                                default:
                                    throw new NotSupportedException($"Entity type '{ctx.Entity.Type}' not supported.");
                            }
                            return Task.Delay(1000);
                        })
                        .ConfigureViewDataPolicy("authority", b => b)
                    );


                });
                watch.Start();
                var rep = scene.DependencyResolver.Resolve<ReplicationApi>();


                var result = new List<Entity>();

                rep.EntitiesChanged += (EntitiesChangedEventArgs batch) =>
                {
                    nbEvents++;
                    var success = true;
                    Debug.WriteLine($"Event : [{myName}] {string.Join(", ", batch.Changes.Select(c => $"{c.ChangeType}:{c.Entity.Id}"))}");

                    foreach (var name in names)
                    {
                        result.Clear();
                        rep.Entities.Query((Entity e, object userState) => e.GetComponent<UserState>("userState").Name == (string)userState, result, name);
                        if (!result.Any())
                        {
                            success = false;
                            break;
                        }
                    }
                    if (success)
                    {
                        watch.Stop();
                        System.Diagnostics.Debug.WriteLine($"synchronized: {watch.ElapsedMilliseconds}ms");

                        tcs.TrySetResult();

                    }


                };

                await rep.WhenAuthoritySynchronized();
                //Create an entity.
                watch.Stop();
                System.Diagnostics.Debug.WriteLine($"Init: {watch.ElapsedMilliseconds}ms");

                watch.Restart();

                var entity = await rep.CreateEntity("user", e =>
                {
                    e.GetComponent<UserState>("userState").Name = myName;
                });

                var cts = new CancellationTokenSource(5000000);

                await await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                entity.BroadcastMessage("test", m => m.IncludeAuthority(false));


                await await Task.WhenAny(msgtcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                var results = new List<Entity>();
                rep.Entities.Query((_, _) => true, results, null);
                ClientFactory.ReleaseClient(clientId);
            }

            await Task.WhenAll(names.Select(name => ReplicateEntity_Impl(ClientIdGenerator.CreateId(), name)));
            //Debug.Assert(nbEvents == names.Count * names.Count);
        }
    }

    public class UserState : ComponentData
    {
        private Action _setDirty;
        private DataStore _currentData = new DataStore();


        public string Name
        {
            get
            {
                return _currentData.Name;
            }
            set
            {
                if (!Entity.IsAuthority)
                {
                    throw new InvalidOperationException("Not authority on the entity.");
                }
                if (_currentData.Name != value)
                {
                    _currentData.Name = value;
                    _setDirty();
                }

            }
        }

        public override void Configure(ComponentConfigurationContext ctx)
        {
            _setDirty = ctx.RegisterReplicationPolicy("userState", config => config
                .ViewPolicy("authority")
                .Reader(ReadDataFrame)
                .Writer(WriteDataFrame)
            );
        }

        public object ReadDataFrame(DataFrameReadContext ctx)
        {

            var store = ctx.Read<DataStore>();
            Debug.Assert(store != null);
            return store;

        }

        public void WriteDataFrame(DataFrameWriteContext ctx)
        {

            ctx.Write(_currentData);


        }

        protected override void OnFrameHistoryUpdated()
        {
            var last = this.InputFrames.Last;
            if (last != null)
            {
                _currentData = last.Content<DataStore>();
            }
        }

        public class DataStore
        {
            public string Name { get; set; }
        }
    }

    public class TimerState : ComponentData
    {

        private long timestamp;
        public override void Configure(ComponentConfigurationContext ctx)
        {
            ctx.RegisterReplicationPolicy("timer", config => config
                .ViewPolicy("authority")
                .Reader(ReadDataFrame)
                .Writer(WriteDataFrame)
                .MinSendInterval(16)
                .AutoSend(true)
                .TriggerPrimaryKeyUpdate()
                );
        }

        public object ReadDataFrame(DataFrameReadContext ctx)
        {
            timestamp = ctx.Read<long>();
            return timestamp;
        }

        public void WriteDataFrame(DataFrameWriteContext ctx)
        {
            timestamp = ctx.Timestamp;
            ctx.Write(ctx.Timestamp);
        }

        protected override T GetPrimaryKeyValue<T>()
        {
            if (timestamp is T v)
            {
                return v;
            }
            else
            {
                throw new InvalidOperationException($"The component primary key is of type {typeof(long)} but was requested {typeof(T)}");
            }
        }
        public override bool SupportsPrimaryKey(out Type type)
        {
            type = typeof(long);
            return true;
        }
    }
}
