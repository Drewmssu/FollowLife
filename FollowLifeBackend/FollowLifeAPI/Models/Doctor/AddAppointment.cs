using System;
using System.ComponentModel.DataAnnotations;

namespace FollowLifeAPI.Models.Doctor
{
    public class AddAppointment
    {
        public int PatientId { get; set ; }
        [Required]
        public DateTime AppointmentDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}