using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models.Patient
{
    public class ActivateMembership
    {
        [Required(AllowEmptyStrings = false)]
        [MinLength(6)]
        [MaxLength(6)]
        public string Code { get; set; } = string.Empty;
    }
}