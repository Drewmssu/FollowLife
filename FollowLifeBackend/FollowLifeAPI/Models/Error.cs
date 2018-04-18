using FollowLifeAPI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FollowLifeAPI.Models
{
    public class Error
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; }

        public Error(string customMessage = null)
        {
            var errorTuple = ErrorHelper.DEFAULT;

            this.Code = errorTuple.Code;
            this.Message = errorTuple.Message;
            this.Details = new List<string>();
        }

        public Error((int Code, string Message) errorTuple, string customMessage = null)
        {
            this.Code = errorTuple.Code;
            this.Message = errorTuple.Message + (string.IsNullOrEmpty(customMessage) ? "" : $" ({customMessage})");
            this.Details = new List<string>();
        }

        public Error((int Code, string Message) errorTuple, List<string> details, string customMessage = null)
        {
            this.Code = errorTuple.Code;
            this.Message = errorTuple.Message + (string.IsNullOrEmpty(customMessage) ? "" : $" ({customMessage})");
            this.Details = details ?? new List<string>();
        }
    }
}