using System.ComponentModel.DataAnnotations;
using System.Web;

namespace FollowLifeAPI.Models.Doctor
{
    public class DProfile
    {        
        [MinLength(9)]
        public string PhoneNumber { get; set; }
        public string MedicalIdentification { get; set; }
        public HttpPostedFileBase ProfilePicture { get; set; }

        //Address
        [MaxLength(5)]
        public string Number { get; set; } = string.Empty;
        [MaxLength(100)]
        public string Complement { get; set; } = string.Empty;
        [MaxLength(100)]
        public string Neighborhood { get; set; } = string.Empty;
        [MaxLength(100)]
        public string Street { get; set; } = string.Empty;
        public int? District { get; set; }
        public int[] MedicalSpecialities { get; set; }
    }
}