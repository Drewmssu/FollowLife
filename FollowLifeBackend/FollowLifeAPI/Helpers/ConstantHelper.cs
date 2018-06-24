using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Helpers
{
    public static class ConstantHelper
    {
        public const string TOKEN_HEADER_NAME = "X-FLLWLF-TOKEN";
        public const int TOKEN_TIMEOUT = 24;
        public static class STATUS
        {
            public const string ACTIVE = "ACT";
            public const string INACTIVE = "INA";
            public const string CONFIRMED = "CON";
            public const string EXPIRED = "EXP";
            public const string REQUESTED = "REQ";
            public const string FINALIZED = "FIN";
            public const string RESCHEDULE_REQUESTED = "RRQ";

            public static string GetStatus(string status)
            {
                switch (status)
                {
                    case ACTIVE: return "Active";
                    case INACTIVE: return "Inactive";
                    case CONFIRMED: return "Confirmed";
                    case EXPIRED: return "Expired";
                    case REQUESTED: return "Requested";
                    case FINALIZED: return "Finalized";
                    case RESCHEDULE_REQUESTED: return "Reschedule_Requested";
                    default: return null;
                }
            }
        }

        public static class ROLE
        {
            public static class ID
            {
                public const int DOCTOR = 1;
                public const int PATIENT = 2;
            }

            public const string PATIENT = "PAT";
            public const string DOCTOR = "DOC";
        }

        public static class PRESCRIPTION_TYPE
        {
            public const string MEDICATION = "MED";
            public const string ACTIVITY = "ACV";
            public const string DIET = "DIE";
            public const string OTHER = "OTH";
        }

        public static class PLAN
        {
            public const string GENERAL_PATIENT = "GEP";
            public const string PREMIUM_PATIENT = "PRP";
            public const string GENERAL_DOCTOR = "GED";
            public const string PREMIUM_DOCTOR = "PRD";
            public const string EXTRA_PLAN = "EXT";

            public static float GetPrice(string plan)
            {
                switch (plan)
                {
                    case GENERAL_PATIENT:
                        return 0.00f;
                    case PREMIUM_PATIENT:
                        return 9.99f;
                    case GENERAL_DOCTOR:
                        return 39.99f;
                    case PREMIUM_DOCTOR:
                        return 59.99f;
                    case EXTRA_PLAN:
                        return 9.99f;
                    default:
                        return 0;
                }
            }
        }

        public static string GetDataConfiguration(string key) => string.IsNullOrEmpty(key) ? string.Empty : ConfigurationManager.AppSettings[key].ToSafeString();
        public static string UniqueShortIdentifier => Guid.NewGuid().ToString().Substring(0, Guid.NewGuid().ToString().IndexOf("-", StringComparison.Ordinal)).ToUpper();
        public enum AppointmentAction
        {
            Confirm = 1,
            Reschedule = 2
        }
    }
}