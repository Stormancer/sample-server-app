using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    internal class UsersTestController : ControllerBase
    {
        private readonly IUserSessions userSessions;

        public UsersTestController(IUserSessions userSessions)
        {
            this.userSessions = userSessions;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task TestSendRequest(RequestContext<IScenePeerClient> ctx)
        {
            var session = await userSessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            await using var pipe = userSessions.SendRequest("a", string.Empty, session.User.Id, ctx.CancellationToken);

            pipe.Writer.Complete();
            pipe.Reader.Complete();
        }
    }
}
