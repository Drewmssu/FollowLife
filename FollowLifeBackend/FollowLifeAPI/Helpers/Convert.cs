using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.ModelBinding;

namespace FollowLifeAPI.Helpers
{
    public static class Convert
    {
        public static string ToSafeString(this object obj, string strDefault = "") => obj?.ToString() ?? strDefault;
        public static string ToTitleCase(this string str) => new System.Globalization.CultureInfo("en-US", false).TextInfo.ToTitleCase(str.ToLower());
        public static List<string> ToListString(this ModelStateDictionary modelState)
        {
            var result = new List<string>();

            var errorMessages = modelState.Values.Select(x => x.Errors.Select(y => new { y.ErrorMessage }));

            foreach (var x in errorMessages)
                foreach (var y in x)
                    result.Add(y.ErrorMessage.ToSafeString());

            result.RemoveAll(string.IsNullOrEmpty);

            return result;
        }
    }
}