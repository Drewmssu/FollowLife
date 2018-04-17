using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FollowLifeBL
{
    public class Cipher
    {
        private static string Salt => string.Join("", ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value.ToUpper().Split('-'));

        public static string Cipher(CipherAction action, CipherType type, string data)
        {
            try
            {
                byte[] iv;
                byte[] key;

                switch (type)
                {
                    case CipherType.UserPassword:
                        iv = Encoding.UTF8.GetBytes("VkYp3s6v9y$B&E(H");
                        key = Encoding.UTF8.GetBytes("(G+KbPeShVmYq3t6w9z$C&F)J@McQfTj");
                        break;
                    case CipherType.Token:
                        iv = Encoding.UTF8.GetBytes(")H@McQfTjWnZr4t7");
                        key = Encoding.UTF8.GetBytes("E(H+KbPeShVmYq3t6w9z$C&F)J@NcQfT");
                        break;
                }

                switch (action)
                {
                    case CipherAction.Encrypt:
                        return Encrypt(iv, key, data);
                    case CipherAction.Decrypt:
                        return Decrypt(iv, key, data);
                    default: return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static string Encrypt (byte[] iv, byte[] key, string plainText)
        {
            var salt = Salt;

            var plainBytes = Encoding.UTF8.GetBytes($"{Salt}{plainText}");

            var rijndael = Rijndael.Create();
            var encryptor = rijndael.CreateEncryptor(key, iv);

            var memoryStream = new MemoryStream(plainBytes.Length);
            var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
            cryptoStream.FlushFinalBlock();

            var cipherBytes = memoryStream.ToArray();

            memoryStream.Close();
            cryptoStream.Close();

            return Convert.ToBase64String(cipherBytes);
        }

        public static string Decrypt (byte[] iv, byte[] key, string cipherText)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = new byte[cipherBytes.Length];

            var rijndael = Rijndael.Create();
            var decryptor = rijndael.CreateDecryptor(key, iv);

            var memoryStream = new MemoryStream(cipherBytes);
            var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

            var decryptedByteCount = cryptoStream.Read(plainBytes, 0, plainBytes.Length);

            memoryStream.Close();
            cryptoStream.Close();

            var plainText = Encoding.UTF8.GetString(plainBytes, 0, decryptedByteCount);
            plainText = plainText.Split(new string[] { Salt }, StringSplitOptions.None)[1];

            return plainText;
        }
    }

    public enum CipherType
    {
        UserPassword,
        Token
    }
    
    public enum CipherAction
    {
        Encrypt,
        Decrypt
    }
}
