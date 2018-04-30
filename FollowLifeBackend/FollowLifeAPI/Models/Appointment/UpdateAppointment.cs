using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using static FollowLifeAPI.Helpers.ConstantHelper;

namespace FollowLifeAPI.Models.Appointment
{
    public class UpdateAppointment
    {
        public DateTime? AppointmentDate { get; set; } = null;
        [EnumDataType(typeof(AppointmentAction))]
        public AppointmentAction Action { get; set; }
    }
}