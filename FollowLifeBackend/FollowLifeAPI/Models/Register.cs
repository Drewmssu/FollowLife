using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models
{
    public class Register
    {
        [Required(AllowEmptyStrings = false)]
        public string FirstName { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        public string LastName { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        public string Email { get; set; } = string.Empty;
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; } = string.Empty;
    }
}