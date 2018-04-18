using FollowLifeAPI.Controllers;
using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using FollowLifeAPI.Models;

namespace FollowLifeAPI.Helpers
{
    public class ErrorHelper
    {
        public static (int Code, string Message) DEFAULT => (Code: 200, Message: "An error ocurred while performing an operation");
        public static (int Code, string Message) STATUS_OK => (Code: 200, Message: "OK");
        public static (int Code, string Message) BAD_REQUEST => (Code: 400, Message: "Bad Request");
        public static (int Code, string Message) UNAUTHORIZED => (Code: 401, Message: "Unauthorized");
        public static (int Code, string Message) FORBIDDEN => (Code: 403, Message: "Forbidden");
        public static (int Code, string Message) NOT_FOUND => (Code: 404, Message: "Not Found");
        public static (int Code, string Message) INTERNAL_ERROR => (Code: 500, Message: "Internal Error");
    }

    public class ErrorResult : BaseController, IHttpActionResult
    {
        Error _error;

        public ErrorResult(string customMessage = null)
        {
            _error = new Error(customMessage);
        }

        public ErrorResult((int Code, string Message) errorTuple, string customMessage = null)
        {
            _error = new Error(errorTuple, customMessage);
        }

        public ErrorResult((int Code, string Message) errorTuple, List<string> details, string customMessage = null)
        {
            _error = new Error(errorTuple, details, customMessage);
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage()
            {
                Content = new ObjectContent<Error>(_error, new JsonMediaTypeFormatter()),
                RequestMessage = Request,
                StatusCode = HttpStatusCode.BadRequest
            };

            return Task.FromResult(response);
        }
    }
}