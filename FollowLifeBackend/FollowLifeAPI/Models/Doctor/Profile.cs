using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models.Doctor
{
    public class Profile
    {
        public HttpPostedFileBase ProfileImage { get; set; }
        public string PhoneNumber { get; set; }

        //Address
        public string Number { get; set; } = string.Empty;
        public string Complement { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public int? District { get; set; }
    }
}