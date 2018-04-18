using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Data.Entity;
using FollowLifeDataLayer;
using FollowLifeLogic;
using FollowLifeAPI.Helpers;

namespace FollowLifeAPI.Controllers
{
    public class BaseController : ApiController
    {
        protected FollowLifeEntities context = new FollowLifeEntities();

        protected int? GetUserId()
        {
            try
            {
                var token = Request.Headers.GetValues(ConstantHelper.TOKEN_HEADER_NAME).First();

                if (!TokenLogic.ValidateToken(token, ConstantHelper.TOKEN_TIMEOUT))
                    return null;

                var user = context.User.FirstOrDefault(x => x.SessionToken == token &&
                                                            x.Status == ConstantHelper.STATUS.CONFIRMED);

                return user?.Id;
            }
            catch
            {
                return null;
            }
        }
    }
}