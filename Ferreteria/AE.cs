using System.Security.Cryptography;
using System.Text;

namespace Ferreteria
{
    public class AE
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789abcdef");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("abcdef9876543210");


        public static string Decrypt(string encryptedText)
        {
            string respuesta = string.Empty;
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            var encryptedBytes = Convert.FromBase64String(encryptedText);

            using var ms = new MemoryStream(encryptedBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using (var sr = new StreamReader(cs))
            {
                respuesta = sr.ReadToEnd();
            }

            return respuesta;

        }
        public static string Encrypt(string plainText)
        {
            string mensaje = string.Empty;
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using var ms = new MemoryStream();

                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }
                var encryptedBytes = ms.ToArray();
                mensaje = Convert.ToBase64String(encryptedBytes);


            }
            return mensaje;
        }
    }
}
