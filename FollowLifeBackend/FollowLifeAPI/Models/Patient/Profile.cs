using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace FollowLifeAPI.Models.Patient
{
    public class Profile
    {
        public HttpPostedFileBase ProfileImage { get; set; }
        [MinLength(9)]
        public string PhoneNumber { get; set; }

    }
}