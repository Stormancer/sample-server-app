using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    internal class RejectConnectionController : ControllerBase
    {
        protected override Task OnConnecting(IScenePeerClient client)
        {
            throw new ClientException("reject");
        }

        
    }
}
