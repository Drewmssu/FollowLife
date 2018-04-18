using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models
{
    public class Login
    {
        [Required(AllowEmptyStrings = false)]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string DeviceToken { get; set; }

        public int UserId { get; set; }
    }
}