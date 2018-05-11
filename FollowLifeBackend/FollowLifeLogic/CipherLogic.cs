using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FollowLifeLogic
{
    public static class CipherLogic
    {
        private static string Salt => string.Join("", ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value.ToUpper().Split('-'));
        public static string Cipher(CipherBCAction action, CipherBCType type, string data)
        {
            try
            {
                byte[] iv;
                byte[] key;

                switch (type)
                {
                    case CipherBCType.UserPassword:
                        iv = Encoding.UTF8.GetBytes("B?E(H+MbQeThWmZq");
                        key = Encoding.UTF8.GetBytes("(H+MbQeThWmZq4t7w!z%C*F-J@NcRfUj");
                        break;
                    case CipherBCType.Token:
                        iv = Encoding.UTF8.GetBytes("aNdRgUkXp2s5v8y/");
                        key = Encoding.UTF8.GetBytes(")H@McQfTjWnZr4u7x!A%D*G-JaNdRgUk");
                        break;
                    default: return null;
                }

                switch (action)
                {
                    case CipherBCAction.Encrypt: return Encrypt(iv, key, data);
                    case CipherBCAction.Decrypt: return Decrypt(iv, key, data);
                    default: return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string Encrypt(byte[] iv, byte[] key, string plainText)
        {
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

        private static string Decrypt(byte[] iv, byte[] key, string cipherText)
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

    public enum CipherBCAction
    {
        Encrypt,
        Decrypt
    }

    public enum CipherBCType
    {
        UserPassword,
        Token
    }
}
