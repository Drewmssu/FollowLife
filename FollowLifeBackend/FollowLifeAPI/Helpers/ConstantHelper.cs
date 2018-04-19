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

        public static string GetDataConfiguration(string key) => string.IsNullOrEmpty(key) ? string.Empty : ConfigurationManager.AppSettings[key].ToSafeString();
        public static string UniqueShortIdentifier => Guid.NewGuid().ToString().Substring(0, Guid.NewGuid().ToString().IndexOf("-", StringComparison.Ordinal)).ToUpper();
    }
}