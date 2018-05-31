using FollowLifeAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace FollowLifeAPI.Extensions
{
    public class ErrorResult : IHttpActionResult
    {
        Response _error;
        HttpRequestMessage _request;

        public ErrorResult(Response error, HttpRequestMessage request)
        {
            _error = error;
            _request = request;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_error.Code)
            {
                Content = new ObjectContent<Response>(_error, new JsonMediaTypeFormatter()),
                RequestMessage = _request
            };

            return Task.FromResult(response);
        }
    }
}