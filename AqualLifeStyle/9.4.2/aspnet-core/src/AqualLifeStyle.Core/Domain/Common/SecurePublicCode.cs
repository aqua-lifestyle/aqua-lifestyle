using System;
using System.Security.Cryptography;

namespace AqualLifeStyle.Domain.Common
{
    public static class SecurePublicCode
    {
        private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string Generate(int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            var characters = new char[length];
            for (var index = 0; index < length; index++)
            {
                characters[index] = Alphabet[bytes[index] & 31];
            }

            return new string(characters);
        }
    }
}
