using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FollowLifeAPI.Models.Appointment
{
    public class AddAppointment
    {
        public int? PatientId { get; set; }
        public int? DoctorId { get; set; } =null;
        [Required]
        public DateTime AppointmentDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}