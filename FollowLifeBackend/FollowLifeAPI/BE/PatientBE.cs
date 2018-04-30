using System.Collections.Generic;
using System.Linq;
using FollowLifeAPI.DataLayer;

namespace FollowLifeAPI.BE
{
    public class PatientBE
    {
        public int PatientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public PatientBE Fill(Patient patient)
        {
            if (patient is null)
                return null;

            this.PatientId = patient.Id;
            this.Name = $"{patient.User.FirstName} {patient.User.LastName}";
            this.PhoneNumber = patient.User.PhoneNumber;
            this.Email = patient.User.Email;

            return this;
        }

        public PatientBE[] Fill(IEnumerable<Patient> patients)
        {
            if (!patients.Any())
                return null;

            var result = new List<PatientBE>();

            foreach (var patient in patients)
                result.Add(new PatientBE().Fill(patient));

            return result.ToArray();
        }
    }
}