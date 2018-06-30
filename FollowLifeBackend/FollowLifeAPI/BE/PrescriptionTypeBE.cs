using FollowLifeAPI.DataLayer;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.BE
{
    public class PrescriptionTypeBE
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;

        public JObject Fill(PrescriptionType pt)
        {
            if (pt is null)
                return null;

            var prescriptionType = new JObject(
                new JProperty("id", pt.Id),
                new JProperty("name", pt.Name));

            return prescriptionType;
        }
    }
}