using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models.Prescription
{
    public class AddPrescription
    {
        public string Frecuency { get; set; } 
        public int? Quantity { get; set; } = null;
        public int? DurationInDays { get; set; } = null;
        [MaxLength(150)]
        public string Description { get; set; } = string.Empty;
        [Required]
        public DateTime StartedAt { get; set; }
        [Required]
        public int PrescriptionTypeId { get; set; }
    }
}