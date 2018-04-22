using FollowLifeAPI.Helpers;
using FollowLifeAPI.Models;
using FollowLifeAPI.Models.Patient;
using FollowLifeDataLayer;
using FollowLifeLogic;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace FollowLifeAPI.Controllers
{
    public class PatientController : BaseController
    {
        [HttpPost]
        [Route("patient/login")]
        public async Task<IHttpActionResult> Login(Login model)
        {
            try
            {
                #region User
                if (model is null)
                    throw new ArgumentException();

                if (!ModelState.IsValid)
                    return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA, ModelState.ToListString());

                #endregion

                #region Validation

                var user = (await context.Doctor.FirstOrDefaultAsync(x => x.User.Email == model.Email.ToLower()))?.User;

                if (user is null)
                    return new ErrorResult(ErrorHelper.NOT_FOUND, "Invalid Identifier");

                if (user.Status == ConstantHelper.STATUS.INACTIVE)
                    return new ErrorResult(ErrorHelper.NOT_FOUND, "Usuario Eliminado");

                if (user.Status != ConstantHelper.STATUS.CONFIRMED ||
                    user.Status != ConstantHelper.STATUS.ACTIVE)
                    return new ErrorResult(ErrorHelper.NOT_FOUND, "User not found");

                #endregion

                #region Login
                if (user.Password != CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password))
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED, "Invalid password");

                #endregion

                if (user.Role.ShortName != ConstantHelper.ROLE.PATIENT)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                if (!string.IsNullOrEmpty(user.SessionToken))
                {
                    if (!TokenLogic.ValidateToken(user.SessionToken, ConstantHelper.TOKEN_TIMEOUT))
                        user.SessionToken = TokenLogic.GenerateToken();
                }
                else
                {
                    user.SessionToken = TokenLogic.GenerateToken();
                }

                user.LastIPConnection = HttpContext.Current.Request.UserHostAddress;
                await context.SaveChangesAsync();

                var device = await context.Device.FirstOrDefaultAsync(x => x.Token == model.DeviceToken);

                if (device is null)
                {
                    context.Device.Add(new Device
                    {
                        UserId = user.Id,
                        Token = model.DeviceToken,
                        CreatedAt = DateTime.Now
                    });

                    await context.SaveChangesAsync();
                }

                return Ok(new
                {
                    user.Id,
                    user.SessionToken,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    ProfileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                    user.PhoneNumber
                });

            }
            catch (ArgumentNullException)
            {
                return new ErrorResult(ErrorHelper.BAD_REQUEST, "Null Request");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex.Message);
            }
        }

        [HttpGet]
        [Route("patient/logout")]
        public async Task<IHttpActionResult> Logout()
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var user = await context.User.FindAsync(userId);

                user.SessionToken = null;

                await context.SaveChangesAsync();

                return Ok();
            }
            catch
            {
                return new ErrorResult();
            }
        }

        [HttpPost]
        [Route("doctor/register")]
        public async Task<IHttpActionResult> Register(Register model)
        {
            try
            {
                if (model is null)
                    return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA, ModelState.ToSafeString());

                #region Validation

                model.Email = model.Email.ToLower();

                if (await context.User.AnyAsync(x => x.Email == model.Email))
                    return new ErrorResult(ErrorHelper.BAD_REQUEST, "Email already exists");

                #endregion

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Password = model.Password,
                    RoleId = ConstantHelper.ROLE.ID.PATIENT,
                    Status = ConstantHelper.STATUS.ACTIVE,
                    CreatedAt = DateTime.Now,
                    LastIPConnection = HttpContext.Current.Request.UserHostAddress,
                    PhoneNumber = model.PhoneNumber
                };

                context.User.Add(user);
                await context.SaveChangesAsync();

                var patient = new Patient
                {
                    UserId = user.Id,
                    Status = ConstantHelper.STATUS.ACTIVE,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                context.Patient.Add(patient);
                await context.SaveChangesAsync();

                model.PatientId = patient.Id;
                model.Password = "### HIDDEN ###";

                return Ok(model);
            }
            catch (ArgumentNullException)
            {
                return new ErrorResult(ErrorHelper.BAD_REQUEST, "Null request data");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex.Message);
            }
        }
    }
}