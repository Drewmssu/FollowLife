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
        public string PhoneNumber { get; set; } = string.Empty;
        [MinLength(8)]
        public string Sex { get; set; } = string.Empty;
        [MinLength(3)]
        public string BloodType { get; set; } = string.Empty;
        [Range(0.10, 9.99)]
        public decimal Height { get; set; }
        [Range(0.10, 999.99)]
        public decimal Weight { get; set; }
        [MaxLength(3)]
        public string Age { get; set; } = string.Empty;
    }
}