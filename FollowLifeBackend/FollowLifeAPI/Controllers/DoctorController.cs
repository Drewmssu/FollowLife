using FollowLifeAPI.Helpers;
using FollowLifeAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using System.Data.Entity;
using FollowLifeLogic;
using FollowLifeDataLayer;
using FollowLifeAPI.Models.Doctor;
using FollowLifeAPI.BE;
using System.Transactions;
using FollowLifeService.MailJet;

namespace FollowLifeAPI.Controllers
{
    public class DoctorController : BaseController
    {
        [HttpPost]
        [Route("doctor/login")]
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

                if (user.Role.ShortName != ConstantHelper.ROLE.DOCTOR)
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
        [Route("doctor/logout")]
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
                    RoleId = ConstantHelper.ROLE.ID.DOCTOR,
                    Status = ConstantHelper.STATUS.ACTIVE,
                    CreatedAt = DateTime.Now,
                    LastIPConnection = HttpContext.Current.Request.UserHostAddress,
                    PhoneNumber = model.PhoneNumber
                };

                context.User.Add(user);
                await context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    UserId = user.Id,
                    Status = ConstantHelper.STATUS.ACTIVE,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                context.Doctor.Add(doctor);
                await context.SaveChangesAsync();

                model.DoctorId = doctor.Id;
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

        [HttpGet]
        [Route("doctor/profile")]
        public async Task<IHttpActionResult> Profile()
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var doctor = user?.Doctor.FirstOrDefault();

                if (doctor != null)
                    return Ok(new
                    {
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        phoneNumber = user.PhoneNumber,
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        email = user.Email,
                        medicIdentification = doctor.MedicIdentification,
                        address = new AddressBE().Fill(doctor.Address),
                        medicalSpeciality = new MedicalSpecialityBE().Fill(doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality)),
                        numberOfPatients = doctor.Membership.Where(x => x.DoctorId == doctor.Id && x.Status == ConstantHelper.STATUS.CONFIRMED).Count()
                    });

                throw new Exception();
            }
            catch
            {
                return new ErrorResult();
            }
        }

        [HttpPut]
        [Route("doctor/profile")]
        public async Task<IHttpActionResult> Profile(Profile model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA, ModelState.ToListString());

                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var doctor = user.Doctor.FirstOrDefault();
                    var address = doctor.Address;

                    user.PhoneNumber = model.PhoneNumber;

                    await context.SaveChangesAsync();

                    if (model.ProfileImage != null)
                    {
                        var image = ImageHelper.UploadImage(model.ProfileImage);
                        if (image != null)
                            user.ProfilePicture = image;
                    }

                    await context.SaveChangesAsync();

                    if (!model.District.HasValue)
                    {
                        if (address is null)
                        {
                            address = new Address
                            {
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            context.Address.Add(address);
                        }
                        else
                        {
                            address.UpdatedAt = DateTime.Now;
                        }

                        address.DistrictId = model.District.Value;
                        address.Street = model.Street;
                        address.Neighborhood = model.Neighborhood;
                        address.Number = model.Number;
                        address.Complement = model.Complement;

                        await context.SaveChangesAsync();
                    }

                    context.DoctorMedicalSpeciality.RemoveRange(doctor.DoctorMedicalSpeciality);
                    await context.SaveChangesAsync();

                    if (model.DoctorMedicalSpeciality.Length > 0)
                    {
                        foreach (var dms in model.DoctorMedicalSpeciality)
                        {
                            context.DoctorMedicalSpeciality.Add(new DoctorMedicalSpeciality
                            {
                                DoctorId = doctor.Id,
                                MedicalSpecialityId = dms
                            });
                        }

                        await context.SaveChangesAsync();
                    }

                    var result = new
                    {
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        phoneNumber = model.PhoneNumber,

                        district = address?.District.Id,
                        street = address?.Street,
                        complement = address?.Complement,
                        number = address?.Number,
                        neighborhood = address.Neighborhood,

                        medicalSpecialities = doctor.DoctorMedicalSpeciality.Select(x => x.Id)
                    };

                    transaction.Complete();

                    return Ok(result);
                }
            }
            catch (ArgumentNullException)
            {
                return new ErrorResult(ErrorHelper.BAD_REQUEST, "Null request");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex.Message);
            }
        }

        [HttpPost]
        [Route("doctor/membership")]
        public async Task<IHttpActionResult> GenerateMembership(GenerateMembership model)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA);

                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var doctor = user.Doctor.FirstOrDefault();

                    model.Email = model.Email.ToLower();

                    var patient = await context.Patient.FirstOrDefaultAsync(x => x.User.Email == model.Email);

                    if (patient is null)
                        return new ErrorResult(ErrorHelper.BAD_REQUEST, "There is no patient with that email");

                    var expirationDate = DateTime.Now.AddHours(24);

                    var membership = new Membership
                    {
                        CreatedAt = DateTime.Now,
                        ExpiresAt = expirationDate,
                        DoctorId = doctor.Id,
                        Status = ConstantHelper.STATUS.ACTIVE,
                        ReferencedEmail = model.Email,
                        Token = TokenLogic.GenerateMembershipToken(),
                    };

                    var emailResult = await MailService.SendMembershipEmail(membership.ReferencedEmail, patient.User.FirstName, membership.Token);

                    if (emailResult != "success")
                        return new ErrorResult(ErrorHelper.BAD_REQUEST, "Email could not be sent, please try again");

                    context.Membership.Add(membership);
                    await context.SaveChangesAsync();

                    transaction.Complete();

                    var result = new
                    {
                        expirationDate = membership.ExpiresAt
                    };

                    return Ok(result);

                }
                catch (ArgumentNullException)
                {
                    return new ErrorResult(ErrorHelper.BAD_REQUEST, "Null request");
                }
                catch (Exception ex)
                {
                    return new ErrorResult(ex.Message);
                }
            }

                
        }


    }

}


