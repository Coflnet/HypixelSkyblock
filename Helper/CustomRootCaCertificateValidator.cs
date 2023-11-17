using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Coflnet.Sky.Core;

/// <summary>
/// Validates a certificate chain using a specific root CA.
/// </summary>
public class CustomRootCaCertificateValidator
{
    private readonly X509Certificate2 _trustedRootCertificateAuthority;

    public CustomRootCaCertificateValidator(X509Certificate2 trustedRootCertificateAuthority)
    {
        _trustedRootCertificateAuthority = trustedRootCertificateAuthority;
    }

    public bool Validate(X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            Console.WriteLine("SSL validation failed due to SslPolicyErrors.RemoteCertificateNotAvailable.");
            return false;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            Console.WriteLine("SSL validation failed due to SslPolicyErrors.RemoteCertificateNameMismatch.");
            return false;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            // verify if the chain is correct
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status == X509ChainStatusFlags.NoError ||
                    status.Status == X509ChainStatusFlags.UntrustedRoot)
                {
                    //Acceptable Status
                }
                else
                {
                    Console.WriteLine(
                        "Certificate chain validation failed. Found chain status {0} ({1}).", status.Status,
                        status.StatusInformation);
                    return false;
                }
            }

            //Now that we have tested to see if the cert builds properly, we now will check if the thumbprint
            //of the root ca matches our trusted one
            var rootCertThumbprint = chain.ChainElements[chain.ChainElements.Count - 1].Certificate.Thumbprint;
            if (rootCertThumbprint != _trustedRootCertificateAuthority.Thumbprint)
            {
                Console.WriteLine(
                    "Root certificate thumbprint mismatch. Expected {0} but found {1}.",
                    _trustedRootCertificateAuthority.Thumbprint, rootCertThumbprint);
                return false;
            }
        }

        return true;
    }
}