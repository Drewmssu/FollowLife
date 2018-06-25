using System.Linq;
using System.Web.Http;
using FollowLifeAPI.Models;
using FollowLifeLogic;
using FollowLifeAPI.Helpers;
using FollowLifeAPI.DataLayer;

namespace FollowLifeAPI.Controllers
{
    public class BaseController : ApiController
    {
        protected FollowLifeEntities context = new FollowLifeEntities();
        protected Response response = new Response();

        protected int? GetUserId()
        {
            try
            {
                var token = Request.Headers.GetValues(ConstantHelper.TOKEN_HEADER_NAME).First();

                if (!TokenLogic.ValidateToken(token, ConstantHelper.TOKEN_TIMEOUT))
                    return null;

                var user = context.User.FirstOrDefault(x => x.SessionToken == token &&
                                                            (x.Status == ConstantHelper.STATUS.CONFIRMED ||
                                                            x.Status == ConstantHelper.STATUS.ACTIVE));

                return user?.Id;
            }
            catch
            {
                return null;
            }
        }
    }
}