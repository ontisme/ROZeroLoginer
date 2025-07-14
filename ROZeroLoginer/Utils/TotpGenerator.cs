using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ROZeroLoginer.Utils
{
    public class TotpGenerator
    {
        private const string BASE32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public string GenerateTotp(string secret, int digits = 6, int period = 30)
        {
            try
            {
                var cleanedSecret = CleanSecret(secret);
                var keyBytes = Base32Decode(cleanedSecret);

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeStep = (ulong)(now / period);

                var timeBytes = BitConverter.GetBytes(timeStep);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(timeBytes);
                }

                var hmac = HmacSha1(keyBytes, timeBytes);

                var offset = hmac[hmac.Length - 1] & 0xf;
                var code = ((hmac[offset] & 0x7f) << 24) |
                          ((hmac[offset + 1] & 0xff) << 16) |
                          ((hmac[offset + 2] & 0xff) << 8) |
                          (hmac[offset + 3] & 0xff);

                var otp = (uint)(code % Math.Pow(10, digits));

                return otp.ToString($"D{digits}");
            }
            catch (Exception ex)
            {
                throw new Exception($"TOTP generation error: {ex.Message}", ex);
            }
        }

        public int GetTimeRemaining(int period = 30)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeInPeriod = (int)(now % period);
            return period - timeInPeriod;
        }

        public bool VerifyTotp(string secret, string inputCode, int tolerance = 1, int digits = 6, int period = 30)
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                for (int i = -tolerance; i <= tolerance; i++)
                {
                    var testTime = now + (i * period);
                    var testTimeStep = (ulong)(testTime / period);

                    var cleanedSecret = CleanSecret(secret);
                    var keyBytes = Base32Decode(cleanedSecret);

                    var timeBytes = BitConverter.GetBytes(testTimeStep);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(timeBytes);
                    }

                    var hmac = HmacSha1(keyBytes, timeBytes);

                    var offset = hmac[hmac.Length - 1] & 0xf;
                    var code = ((hmac[offset] & 0x7f) << 24) |
                              ((hmac[offset + 1] & 0xff) << 16) |
                              ((hmac[offset + 2] & 0xff) << 8) |
                              (hmac[offset + 3] & 0xff);

                    var otp = (uint)(code % Math.Pow(10, digits));
                    var expectedCode = otp.ToString($"D{digits}");

                    if (inputCode == expectedCode)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string CleanSecret(string secret)
        {
            var cleaned = new StringBuilder();

            foreach (char c in secret)
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(char.ToUpper(c));
                }
            }

            var result = new StringBuilder();
            foreach (char c in cleaned.ToString())
            {
                if (BASE32_ALPHABET.Contains(c))
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        private byte[] Base32Decode(string base32)
        {
            var cleanInput = base32.TrimEnd('=');
            var bits = new StringBuilder();

            foreach (char c in cleanInput)
            {
                var pos = BASE32_ALPHABET.IndexOf(c);
                if (pos == -1)
                {
                    throw new ArgumentException($"Invalid base32 character: {c}");
                }

                var binary = Convert.ToString(pos, 2).PadLeft(5, '0');
                bits.Append(binary);
            }

            var bytes = new List<byte>();
            for (int i = 0; i + 8 <= bits.Length; i += 8)
            {
                var byteStr = bits.ToString(i, 8);
                var byteValue = Convert.ToByte(byteStr, 2);
                bytes.Add(byteValue);
            }

            return bytes.ToArray();
        }

        private byte[] HmacSha1(byte[] key, byte[] message)
        {
            using (var hmac = new HMACSHA1(key))
            {
                return hmac.ComputeHash(message);
            }
        }
    }
}