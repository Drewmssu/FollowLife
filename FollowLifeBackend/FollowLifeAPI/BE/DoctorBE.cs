using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FollowLifeDataLayer;

namespace FollowLifeAPI.BE
{
    public class DoctorBE
    {
        public int DoctorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string[] MedicalSpecialities { get; set; }

        public DoctorBE Fill(Doctor doctor)
        {
            if (doctor is null)
                return null;

            this.DoctorId = doctor.Id;
            this.Name = doctor.User.FirstName + doctor.User.LastName;
            this.PhoneNumber = doctor.User.PhoneNumber;
            this.Email = doctor.User.Email;
            this.MedicalSpecialities = (string[])doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality.Name);

            return this;
        }

    }
}