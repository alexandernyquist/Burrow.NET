﻿using System;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
namespace Burrow.Tests.ConnectionStringTests
{
    [TestFixture]
    public class Constructor
    {
        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void Should_throw_excepton_if_provide_null_connection_string()
        {
            new ConnectionString(null);
        }

        [Test, ExpectedException(typeof(Exception))]
        public void Should_throw_excepton_if_provide_invalid_connection_string()
        {
            new ConnectionString("a=b;c=d=e");
        }

        [Test]
        public void Should_be_able_to_regconize_the_port_number()
        {
            var connection = new ConnectionString("host=localhost:1234;username=guest;password=guest");
            Assert.AreEqual(1234, connection.Port);
        }

        [Test]
        public void Should_be_able_to_regconize_the_virtualHost()
        {
            var connection = new ConnectionString("host=localhost:1234;username=guest;password=guest;virtualHost=UAT");
            Assert.AreEqual("UAT", connection.VirtualHost);
        }
    }
}
// ReSharper restore InconsistentNaming}
