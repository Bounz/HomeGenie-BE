// 
//  StringCipher.cs
//  
//  Author:
//       http://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp
// 

using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using NLog;

namespace HomeGenie.Service
{
    public static class StringCipher
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // This constant string is used as a "salt" value for the PasswordDeriveBytes function calls.
        // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
        // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
        private const string InitVector = "h7g3e4m3t5st5zjw";

        // This constant is used to determine the keysize of the encryption algorithm.
        private const int KeySize = 256;

        public static string Encrypt(string plainText, string passPhrase)
        {
            var initVectorBytes = Encoding.UTF8.GetBytes(InitVector);
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var password = new PasswordDeriveBytes(passPhrase, null);
            var keyBytes = password.GetBytes(KeySize / 8);
            var symmetricKey = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.Zeros
            };
            var encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            var memoryStream = new MemoryStream();
            var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            var cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }

        public static string Decrypt(string cipherText, string passPhrase)
        {
            try
            {
                return Decrypt(cipherText, passPhrase, PaddingMode.PKCS7);
            }
            catch (CryptographicException e)
            {
                Log.Warn($"Error decrypting string \"{cipherText}\" with padding mode PKCS7, will try other paddings...");
                Log.Warn(e);
            }
            catch (Exception)
            {
                Log.Warn($"Error decrypting string \"{cipherText}\"");
                throw;
            }

            var otherPaddingModes = Enum.GetValues(typeof(PaddingMode)).Cast<PaddingMode>();
            foreach (var paddingMode in otherPaddingModes)
            {
                try
                {
                    return Decrypt(cipherText, passPhrase, paddingMode);
                }
                catch (Exception e)
                {
                    Log.Warn($"Error decrypting string \"{cipherText}\" with padding mode {paddingMode}");
                    Log.Warn(e);
                }
            }

            throw new InvalidOperationException($"Can't decrypt string \"{cipherText}\"");
        }

        private static string Decrypt(string cipherText, string passPhrase, PaddingMode paddingMode)
        {
            var initVectorBytes = Encoding.ASCII.GetBytes(InitVector);
            var cipherTextBytes = Convert.FromBase64String(cipherText);
            var password = new PasswordDeriveBytes(passPhrase, null);
            var keyBytes = password.GetBytes(KeySize / 8);
            var symmetricKey = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                Padding = paddingMode
            };
            var decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            var memoryStream = new MemoryStream(cipherTextBytes);
            var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            var plainTextBytes = new byte[cipherTextBytes.Length];
            cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();

            var plainTextBytesList = plainTextBytes.ToList();
            plainTextBytesList.RemoveAll(x => x == 0);
            return Encoding.UTF8.GetString(plainTextBytesList.ToArray());
        }
    }
}
