using FollowLifeAPI.DataLayer;
using FollowLifeAPI.Helpers;
using Newtonsoft.Json.Linq;

namespace FollowLifeAPI.BE
{
    public class UserBE
    {
        public JObject Fill(User u)
        {
            if (u is null)
                return null;

            var user = new JObject(
                new JProperty("firstName", u.FirstName),
                new JProperty("lastName", u.LastName),
                new JProperty("phoneNumber", u.PhoneNumber),
                new JProperty("profileImage", ImageHelper.GetImageURL(u.ProfilePicture)),
                new JProperty("email", u.Email));

            return user;
        }
    }
}