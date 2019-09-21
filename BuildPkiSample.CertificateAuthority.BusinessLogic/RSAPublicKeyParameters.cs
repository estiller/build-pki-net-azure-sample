namespace BuildPkiSample.CertificateAuthority.BusinessLogic
{
    public class RSAPublicKeyParameters
    {
        public RSAPublicKeyParameters(byte[] exponent, byte[] modulus)
        {
            Exponent = exponent;
            Modulus = modulus;
        }

        public byte[] Exponent { get; }
        public byte[] Modulus { get; }
    }
}