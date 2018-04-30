using FollowLifeAPI.DataLayer;
using System.Collections.Generic;
using System.Linq;

namespace FollowLifeAPI.BE
{
    public class MedicalSpecialityBE
    {
        public int MedicalSpecialityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public MedicalSpecialityBE Fill(MedicalSpeciality me)
        {
            if (me is null)
                return null;

            this.MedicalSpecialityId = me.Id;
            this.Name = me.Name;
            this.Code = me.Code;

            return this;
        }

        public MedicalSpecialityBE[] Fill(IEnumerable<MedicalSpeciality> mes)
        {
            if (!mes.Any())
                return null;

            var result = new List<MedicalSpecialityBE>();

            foreach (var m in mes)
                result.Add(new MedicalSpecialityBE().Fill(m));

            return result.ToArray();
        }
    }
}