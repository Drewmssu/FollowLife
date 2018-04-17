using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FollowLifeHelpers
{
    public static class ConstantHelper
    {
        public static class STATUS
        {
            public const string ACTIVE = "ACT";
            public const string INACTIVE = "INA";
        }

        public static class ROLE
        {
            public const string PATIENT = "PAT";
            public const string DOCTOR = "DOC";
        }

        public static class ITEMTYPE
        {
            public const string MEDICATION = "MED";
            public const string ACTIVITY = "ACV";
            public const string DIET = "DIE";
        }

        public static class PLAN
        {
            public const string GENERAL = "GEN";
            public const string PRO = "PRO";
        }
    }
}
