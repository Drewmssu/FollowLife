using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models.Doctor
{
    public class GenerateMembership
    {
        [Required(AllowEmptyStrings = false)]
        public string Email { get; set; } = string.Empty;
    }
}