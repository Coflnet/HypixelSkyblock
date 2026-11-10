using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Coflnet.Sky.Core;
using Coflnet.Sky.Core.Archive;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using NUnit.Framework;

namespace Tests;

[NonParallelizable]
public class DatabaseTlsFactoryTests
{
    private const string Legacy = "Server=legacy;User ID=svc;Password=synthetic-only;SslMode=None;ConnectionTimeout=90";
    private string dir;

    [SetUp]
    public void Setup()
    {
        HypixelContext.ConnectionFactory = null;
        dir = Path.Combine(Path.GetTempPath(), "dbtls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void Cleanup()
    {
        HypixelContext.ConnectionFactory = null;
        Directory.Delete(dir, true);
    }

    [Test]
    public void WithoutTlsSectionNoFactoryAndConnectionUnchanged()
    {
        HypixelContext.SetConfiguration(Configuration(new()));
        Assert.That(HypixelContext.ConnectionFactory, Is.Null);
        using var connection = new DatabaseTlsFactory(Configuration(new())).CreateConnection(Legacy);
        var options = new MySqlConnectionStringBuilder(connection.ConnectionString);
        Assert.Multiple(() =>
        {
            Assert.That(options.SslMode, Is.EqualTo(MySqlSslMode.None));
            Assert.That(options.ConnectionTimeout, Is.EqualTo(90));
            Assert.That(connection.RemoteCertificateValidationCallback, Is.Null);
            Assert.That(connection.ProvideClientCertificatesCallback, Is.Null);
        });
    }

    [Test]
    public void SetConfigurationWithTlsInstallsFactoryWithTlsOptions()
    {
        HypixelContext.SetConfiguration(Configuration(CompleteKeys()));
        Assert.That(HypixelContext.ConnectionFactory, Is.Not.Null);
        using var connection = HypixelContext.ConnectionFactory(Legacy + ";SslCa=old.pem;ConnectionLifeTime=0");
        var options = new MySqlConnectionStringBuilder(connection.ConnectionString);
        Assert.Multiple(() =>
        {
            Assert.That(options.SslMode, Is.EqualTo(MySqlSslMode.Required));
            Assert.That(options.Password, Is.EqualTo("synthetic-only"));
            Assert.That(options.ConnectionTimeout, Is.EqualTo(10));
            Assert.That(options.ConnectionLifeTime, Is.EqualTo(300));
            Assert.That(options.SslCa, Is.Empty);
            Assert.That(connection.RemoteCertificateValidationCallback, Is.Not.Null);
            Assert.That(connection.ProvideClientCertificatesCallback, Is.Not.Null);
        });
    }

    [Test]
    public void SmallerTimeoutsAreKept()
    {
        var tls = new DatabaseTlsFactory(Configuration(CompleteKeys()));
        using var connection = tls.CreateConnection(Legacy + ";ConnectionTimeout=3;ConnectionLifeTime=60");
        var options = new MySqlConnectionStringBuilder(connection.ConnectionString);
        Assert.That((options.ConnectionTimeout, options.ConnectionLifeTime), Is.EqualTo((3u, 60u)));
    }

    [Test]
    public void ClientCallbackAddsCertificateFromPemFiles()
    {
        var values = WritePemFiles();
        var tls = new DatabaseTlsFactory(Configuration(values));
        using var connection = tls.CreateConnection(Legacy);
        var certificates = new X509CertificateCollection();
        connection.ProvideClientCertificatesCallback(certificates);
        Assert.That(certificates, Has.Count.EqualTo(1));
    }

    [TestCase("CA_CERT_PATH")]
    [TestCase("CERT_PATH")]
    [TestCase("KEY_PATH")]
    public void MissingKeyThrowsNamingIt(string missing)
    {
        var values = CompleteKeys();
        values.Remove("DB:TLS:" + missing);
        var ex = Assert.Throws<InvalidOperationException>(() => HypixelContext.SetConfiguration(Configuration(values)));
        Assert.That(ex.Message, Does.Contain("DB:TLS:" + missing));
    }

    [Test]
    public void TlsSectionWithoutSharedContextOptInInstallsNothing()
    {
        var values = CompleteKeys();
        values.Remove("DB:TLS:SHARED_CONTEXT");
        HypixelContext.SetConfiguration(Configuration(values));
        ArchiveContext.SetConfiguration(Configuration(values));
        Assert.That(HypixelContext.ConnectionFactory, Is.Null);
    }

    [Test]
    public void IncompleteTlsSectionWithoutOptInDoesNotThrow()
    {
        var values = new Dictionary<string, string> { ["DB:TLS:CA_CERT_PATH"] = "only-ca.pem" };
        Assert.DoesNotThrow(() => HypixelContext.SetConfiguration(Configuration(values)));
        Assert.That(HypixelContext.ConnectionFactory, Is.Null);
    }

    [Test]
    public void ExistingFactoryIsNotOverwritten()
    {
        Func<string, MySqlConnection> own = s => new MySqlConnection(s);
        HypixelContext.ConnectionFactory = own;
        HypixelContext.SetConfiguration(Configuration(CompleteKeys()));
        Assert.That(HypixelContext.ConnectionFactory, Is.SameAs(own));
    }

    [Test]
    public void ArchiveContextSetConfigurationInstallsFactory()
    {
        ArchiveContext.SetConfiguration(Configuration(CompleteKeys()));
        Assert.That(HypixelContext.ConnectionFactory, Is.Not.Null);
    }

    private Dictionary<string, string> WritePemFiles()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var ca = Path.Combine(dir, "ca.pem");
        var crt = Path.Combine(dir, "client.pem");
        var key = Path.Combine(dir, "client.key");
        File.WriteAllText(ca, cert.ExportCertificatePem());
        File.WriteAllText(crt, cert.ExportCertificatePem());
        File.WriteAllText(key, rsa.ExportPkcs8PrivateKeyPem());
        return new()
        {
            ["DB:TLS:CA_CERT_PATH"] = ca,
            ["DB:TLS:CERT_PATH"] = crt,
            ["DB:TLS:KEY_PATH"] = key,
        };
    }

    private static Dictionary<string, string> CompleteKeys() => new()
    {
        ["DB:TLS:SHARED_CONTEXT"] = "true",
        ["DB:TLS:CA_CERT_PATH"] = "missing-roots.pem",
        ["DB:TLS:CERT_PATH"] = "missing-client.pem",
        ["DB:TLS:KEY_PATH"] = "missing-client.key",
    };

    private static IConfiguration Configuration(Dictionary<string, string> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
