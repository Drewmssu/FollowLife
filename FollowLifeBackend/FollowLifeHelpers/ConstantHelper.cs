using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FollowLifeHelpers
{
    public static class ConstantHelper
    {
        public static class ROL
        {
            public const string DOCTOR = "DOC";
            public const string PATIENT = "PAT";
        }

        public static class STATUS
        {
            public const string ACTIVE = "ACT";
            public const string INACTIVE = "INA";
        }

        public static class PLAN
        {
            public const string GENERAL = "GEN";
            public const string PRO = "PRO";
        }

        public static class ITEMTYPE
        {
            public const string PILL = "PIL";
            public const string ACTIVITY = "ACV";
            public const string DIET = "DIE";
        }
    }
}
