using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Tests
{
    internal static class ClientIdGenerator
    {
        private static int _currentId = 0;
        public static int CreateId() => _currentId++;
    }
}
