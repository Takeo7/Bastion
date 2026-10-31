using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Bastion.Net;

namespace Bastion.Relay
{
    /// <summary>
    /// The relay as a program (docs/UNIRSE-CON-CODIGO.md):
    ///   Bastion.Relay --puerto 443 --clave /var/lib/bastion-rele/clave --certificado rele.pfx
    ///   Bastion.Relay --direccion 127.0.0.1 --puerto 41401 --clave clave [--retraso 80]   (the local tests: no TLS)
    ///   Bastion.Relay --crear-certificado rele.pfx   (once: our own certificate, and the key the apps carry)
    /// --clave: the file with what signs the codes (made the first time); kept, codes survive a restart.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var o = new RelayOptions();
            string? keyFile = null;
            for (var i = 0; i < args.Length; i++)
            {
                var next = i + 1 < args.Length ? args[i + 1] : "";
                switch (args[i])
                {
                    case "--puerto":
                        o.Port = int.Parse(next);
                        i++;
                        break;
                    case "--direccion":
                        o.Address = IPAddress.Parse(next);
                        i++;
                        break;
                    case "--clave":
                        keyFile = next;
                        i++;
                        break;
                    case "--certificado":
                        o.Certificate = X509CertificateLoader.LoadPkcs12FromFile(next, null);
                        i++;
                        break;
                    case "--retraso":
                        o.DelayMs = int.Parse(next);
                        i++;
                        break;
                    case "--crear-certificado":
                        MakeCertificate(next);
                        return 0;
                    default:
                        Console.Error.WriteLine($"No sé qué es «{args[i]}».");
                        return 2;
                }
            }
            if (keyFile != null) o.Key = KeyFrom(keyFile);
            else Console.WriteLine("sin --clave: los códigos no sobreviven a un reinicio");
            using var relay = new RelayServer(o, (line) => Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {line}"));
            relay.Start();
            var quit = new ManualResetEventSlim();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                quit.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (_, __) => quit.Set();
            quit.Wait();
            return 0;
        }

        /// <summary>The signing key in `path`, made there (only its owner reads it) if it isn't yet.</summary>
        private static byte[] KeyFrom(string path)
        {
            if (File.Exists(path)) return File.ReadAllBytes(path);
            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(path, key);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return key;
        }

        /// <summary>Our own certificate, for 20 years (RSA 2048, which every TLS speaks), and its key as the apps carry it.</summary>
        private static void MakeCertificate(string path)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Bastion rele", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
            File.WriteAllBytes(path, cert.Export(X509ContentType.Pkcs12));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Console.WriteLine($"certificado en {path}; la clave para las apps (RelayAddress): {RelayWire.KeyOf(cert)}");
        }
    }
}
