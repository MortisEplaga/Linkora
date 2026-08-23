using System.Security.Cryptography;

namespace Linkora.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string storedHash);
        bool IsLegacyHash(string storedHash);
    }

    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

        private const string Prefix = "PBKDF2";

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
            return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        public bool Verify(string password, string storedHash)
        {
            if (IsLegacyHash(storedHash))
                return VerifyLegacy(password, storedHash);

            var parts = storedHash.Split('.');
            if (parts.Length != 4 || parts[0] != Prefix) return false;

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expectedKey = Convert.FromBase64String(parts[3]);

            var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);
            return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
        }

        public bool IsLegacyHash(string storedHash)
        {
            return storedHash.Length == 64 && !storedHash.Contains('.');
        }

        private static bool VerifyLegacy(string password, string storedHash)
        {
            var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
            var computed = Convert.ToHexString(bytes).ToLower();
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(computed),
                System.Text.Encoding.UTF8.GetBytes(storedHash));
        }
    }
}