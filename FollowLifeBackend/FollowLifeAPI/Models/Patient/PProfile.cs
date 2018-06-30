using System.ComponentModel.DataAnnotations;
using System.Web;

namespace FollowLifeAPI.Models.Patient
{
    public class PProfile
    {
        public HttpPostedFileBase ProfileImage { get; set; }
        [MinLength(9)]
        public string PhoneNumber { get; set; }

        [MaxLength(8)]
        [MinLength(1)]
        public string Sex { get; set; }
        [MinLength(2)]
        public string BloodType { get; set; }
        [Range(0.10, 9.99)]
        public decimal? Height { get; set; }
        [Range(0.10, 999.99)]
        public decimal? Weight { get; set; }
        [MaxLength(3)]
        public string Age { get; set; }
    }
}