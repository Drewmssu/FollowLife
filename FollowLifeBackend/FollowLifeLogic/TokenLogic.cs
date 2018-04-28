using System;
using System.Linq;

namespace FollowLifeLogic
{
    public static class TokenLogic
    {
        public static string GenerateToken()
        {
            var time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            var key = Guid.NewGuid().ToByteArray();
            var token = Convert.ToBase64String(time.Concat(key).ToArray());

            token = CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.Token, token);

            return token;
        }

        //expiration time = 6 months
        public static bool ValidateToken(string token, int maxValidHours = 365 * 4320)
        {
            try
            {
                token = CipherLogic.Cipher(CipherBCAction.Decrypt, CipherBCType.Token, token);

                var data = Convert.FromBase64String(token);
                var createdAt = DateTime.FromBinary(BitConverter.ToInt64(data, 0));

                return createdAt > DateTime.UtcNow.AddHours(maxValidHours * -1);
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateMembershipToken()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, 6)
                .Select(x => x[random.Next(x.Length)]).ToArray());
        }
    }
}
