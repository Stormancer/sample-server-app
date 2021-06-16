using Stormancer.Plugins;


namespace Stormancer.Server.TestApp
{
    class TestPlugin : IHostPlugin
    {


        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
            

            };
            ctx.HostStarting += (IHost host) =>
            {

                host.ConfigureUsers(u => u.ConfigureEphemeral(b => b.Enabled()));
                
            };

            ctx.HostStarted += (IHost host) =>
            {



            };


        }
    }
}
