﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    [TestFixture]
    public class WebSocketExtensionTests
    {
        [Test]
        public async Task GetInputStreamReaderTest()
        {
            var res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 100).ToArray()).GetInputStreamReader();
            Assert.IsTrue(res.ToArray().All(v => v == 1));
            res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 1000).ToArray()).GetInputStreamReader();
            Assert.IsTrue(res.ToArray().All(v => v == 1));
            Assert.ThrowsAsync<OutOfMemoryException>(async () => await new FakeWebSocket(Enumerable.Repeat((byte)1, 20000).ToArray()).GetInputStreamReader());
        }
    }
}
