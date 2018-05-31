using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

namespace FollowLifeAPI.Models
{
    public class Response
    {
        public string Status { get; set; } = string.Empty;
        public HttpStatusCode Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public object Result { get; set; } = null;
    }
}