using USBAnywhere.Utils;
using Xunit;

namespace USBAnywhere.Tests;

public class InputValidatorTests
{
    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    [InlineData("raspberrypi")]
    [InlineData("my-server.local")]
    [InlineData("a.b.c")]
    public void Host_Valid(string host) => Assert.True(InputValidator.IsValidHost(host));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("1.2.3.4 -b 1-1")]
    [InlineData("1.2.3.4;calc")]
    [InlineData("-oProxyCommand=calc")]
    [InlineData("host&whoami")]
    [InlineData("host|dir")]
    [InlineData("fe80::1%eth0")]
    [InlineData(" 1.2.3.4")]
    [InlineData("host name")]
    [InlineData("\"quoted\"")]
    public void Host_Invalid(string? host) => Assert.False(InputValidator.IsValidHost(host));

    [Theory]
    [InlineData("1-1")]
    [InlineData("3-2")]
    [InlineData("1-2.4")]
    [InlineData("12-1.2.3")]
    public void BusId_Valid(string id) => Assert.True(InputValidator.IsValidBusId(id));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1-1 -r evil")]
    [InlineData("-b")]
    [InlineData("1-1;x")]
    [InlineData("abc")]
    public void BusId_Invalid(string? id) => Assert.False(InputValidator.IsValidBusId(id));

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("127")]
    public void Port_Valid(string p) => Assert.True(InputValidator.IsValidPort(p));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-1")]
    [InlineData("1 2")]
    [InlineData("1234")]
    [InlineData("a")]
    public void Port_Invalid(string? p) => Assert.False(InputValidator.IsValidPort(p));

    [Theory]
    [InlineData("pi")]
    [InlineData("user_1")]
    [InlineData("john.doe")]
    public void SshUser_Valid(string u) => Assert.True(InputValidator.IsValidSshUser(u));

    [Theory]
    [InlineData("-oProxyCommand=x")]
    [InlineData("a b")]
    [InlineData("a&b")]
    [InlineData("")]
    [InlineData(null)]
    public void SshUser_Invalid(string? u) => Assert.False(InputValidator.IsValidSshUser(u));
}
