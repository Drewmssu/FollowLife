using FollowLifeAPI.Helpers;
using FollowLifeAPI.Models;
using FollowLifeLogic;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Web;
using System.Web.Http;
using FollowLifeAPI.BE;
using FollowLifeAPI.Models.Appointment;
using System.Net;
using FollowLifeAPI.Extensions;
using FollowLifeAPI.Models.Patient;
using FollowLifeAPI.DataLayer;

namespace FollowLifeAPI.Controllers
{
    [RoutePrefix("api/v1/patients")]
    public class PatientController : BaseController
    {
        [HttpPost]
        [Route("login")]
        public async Task<IHttpActionResult> Login(Login model)
        {
            try
            {
                #region User
                if (model is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = new ArgumentNullException().Message;
                    return new ErrorResult(response, Request);
                }

                if (!ModelState.IsValid)
                {
                    response.Code = HttpStatusCode.NoContent;
                    response.Status = "error";
                    response.Message = ModelState.ToString();
                    return new ErrorResult(response, Request);
                }

                #endregion

                #region Validation

                var user = (await context.Patient.FirstOrDefaultAsync(x => x.User.Email == model.Email.ToLower()))?.User;

                if (user is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Invalid identifier";
                    return new ErrorResult(response, Request);
                }

                if (user.Status == ConstantHelper.STATUS.INACTIVE)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "User has been deleted";
                    return new ErrorResult(response, Request);
                }

                if (user.Status != ConstantHelper.STATUS.CONFIRMED &&
                    user.Status != ConstantHelper.STATUS.ACTIVE)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "User not found";
                    return new ErrorResult(response, Request);
                }

                #endregion

                #region Login
                if (user.Password != CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password))
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Invalid password";
                    return new ErrorResult(response, Request);
                }

                #endregion

                if (user.Role.ShortName != ConstantHelper.ROLE.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

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

                response.Result = new
                {
                    user.Patient.FirstOrDefault(x => x.UserId == user.Id).Id,
                    user.SessionToken,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    ProfileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                    user.PhoneNumber
                };
                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }
            catch (ArgumentNullException)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = new ArgumentNullException().Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpGet]
        [Route("logout")]
        public async Task<IHttpActionResult> Logout()
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                user.SessionToken = null;

                await context.SaveChangesAsync();
                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }
            catch
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "An error has ocurred";
                return new ErrorResult(response, Request);
            }
        }

        [HttpPost]
        [Route("register")]
        public async Task<IHttpActionResult> Register(Register model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    #region Validation

                    model.Email = model.Email.ToLower();

                    if (await context.User.AnyAsync(x => x.Email == model.Email))
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Email already exist";
                        return new ErrorResult(response, Request);
                    }

                    #endregion

                    var user = new User
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email.ToLower(),
                        Password = CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password),
                        RoleId = ConstantHelper.ROLE.ID.PATIENT,
                        Status = ConstantHelper.STATUS.ACTIVE,
                        CreatedAt = DateTime.Now,
                        LastIPConnection = HttpContext.Current.Request.UserHostAddress
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
                    transaction.Complete();

                    response.Code = HttpStatusCode.Created;
                    response.Message = "success";

                    return Ok(response);
                }
            }
            catch (ArgumentNullException)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = new ArgumentNullException().Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpGet]
        [Route("{patientId}")]
        public async Task<IHttpActionResult> Profile(int patientId)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null || patient.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                response.Result = new
                {
                    user = new UserBE().Fill(patient.User),
                    weight = patient.Weight,
                    height = patient.Height,
                    age = patient.Age,
                    bloodType = patient.BloodType,
                    sex = patient.Sex
                };
                response.Code = HttpStatusCode.OK;
                response.Status = "ok";

                return Ok(response);
            }
            catch
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "An error has ocurred";
                return new ErrorResult(response, Request);
            }
        }

        [HttpPut]
        [Route("{patientId}")]
        public async Task<IHttpActionResult> Profile(int patientId, PProfile model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = new ArgumentNullException().Message;
                        return new ErrorResult(response, Request);
                    }

                    if (!ModelState.IsValid)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var userId = GetUserId();

                    if (userId is null)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null || patient.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }
                    if (model.PhoneNumber != null)
                        user.PhoneNumber = model.PhoneNumber;

                    user.UpdatedOn = DateTime.Now;

                    if (!string.IsNullOrEmpty(model.Age))
                        patient.Age = model.Age;

                    if (model.Weight != null)
                        patient.Weight = model.Weight;

                    if (model.Height != null)
                        patient.Height = model.Height;

                    if (string.IsNullOrEmpty(model.BloodType))
                        patient.BloodType = model.BloodType;

                    if (string.IsNullOrEmpty(model.Sex))
                        patient.Sex = model.Sex;

                    patient.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    if (model.ProfileImage != null)
                    {
                        var image = ImageHelper.UploadImage(model.ProfileImage);
                        if (image != null)
                            user.ProfilePicture = image;
                    }

                    await context.SaveChangesAsync();

                    response.Result = new
                    {
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        phoneNumber = user.PhoneNumber,
                        age = patient.Age,
                        weight = patient.Weight,
                        height = patient.Height,
                        sex = patient.Sex,
                        bloodType = patient.BloodType,
                    };
                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    transaction.Complete();

                    return Ok(response);
                }
            }
            catch (ArgumentNullException e)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = e.Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpPut]
        [Route("{patientId}/membership")]
        public async Task<IHttpActionResult> ActivateMembership(int patientId, ActivateMembership model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = new ArgumentNullException().Message;
                        return new ErrorResult(response, Request);
                    }

                    if (!ModelState.IsValid)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var userId = GetUserId();

                    if (userId is null)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null || patient.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    model.Code = model.Code.ToUpper();
                    var membership = await context.Membership.FirstOrDefaultAsync(x => x.ReferencedEmail == user.Email &&
                                                                                       x.Token == model.Code &&
                                                                                       x.Status == ConstantHelper.STATUS.ACTIVE);

                    if (membership is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Membership not found";
                        return new ErrorResult(response, Request);
                    }

                    if (membership.ExpiresAt < DateTime.Now)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Your code has expired";
                        return new ErrorResult(response, Request);
                    }

                    membership.PatientId = patient.Id;
                    membership.Status = ConstantHelper.STATUS.CONFIRMED;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    response.Status = "ok";
                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    return Ok(response);
                }
            }
            catch (ArgumentNullException e)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = e.Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpGet]
        [Route("{patientId}/doctors")]
        [Route("{patientId}/doctors/{doctorId}")]
        public async Task<IHttpActionResult> GetDoctors(int patientId, int? doctorId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null || patient.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                if (doctorId.HasValue)
                {
                    var doctor = context.Membership.FirstOrDefault(x => x.DoctorId == doctorId &&
                                                                         x.PatientId == patient.Id &&
                                                                         x.Status == ConstantHelper.STATUS.CONFIRMED)?.Doctor;

                    if (doctor is null)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Doctor does not exist");

                    //TODO: Validate if doctor membership is active (up to date on his payments)
                    //TODO: Returns indicator (IMPORTANT)
                    response.Result = new
                    {
                        profileImage = ImageHelper.GetImageURL(doctor.User.ProfilePicture),
                        name = doctor.User.FirstName,
                        lastName = doctor.User.LastName,
                        medicIndentification = doctor.MedicIdentification,
                        address = new AddressBE().Fill(doctor.Address),
                        medicalSpecialities = new MedicalSpecialityBE().Fill(doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality))
                    };
                    response.Code = HttpStatusCode.OK;
                    response.Status = "ok";


                    return Ok(response);
                }

                response.Result = patient.Membership.Where(x => x.PatientId == patient.Id &&
                                                           x.Status == ConstantHelper.STATUS.CONFIRMED)
                    .Select(x => new
                    {
                        id = x.DoctorId,
                        name = x.Doctor.User.FirstName + " " + x.Doctor.User.LastName,
                        profileImage = ImageHelper.GetImageURL(x.Doctor.User.ProfilePicture)
                    }).ToList();

                response.Code = HttpStatusCode.OK;
                response.Status = "ok";

                return Ok(response);
            }
            catch
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "An error has ocurred";
                return new ErrorResult(response, Request);
            }
        }

        [HttpGet]
        [Route("{patientId}/appointments")]
        [Route("{patientId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> GetAppointments(int patientId, int? appointmentId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null || patient.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                if (appointmentId.HasValue)
                {
                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Appointment does not exist";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Appointment not available";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.PatientId != patient.Id)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Current user is not part of this appointment";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.AppointmentDate < DateTime.Now)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Appointment has expired";
                        return new ErrorResult(response, Request);
                    }

                    response.Code = HttpStatusCode.OK;
                    response.Status = "ok";
                    response.Result = new
                    {
                        createdAt = appointment.CreatedAt,
                        appointmentDate = appointment.AppointmentDate,
                        reason = appointment.Reason,
                        status = ConstantHelper.STATUS.GetStatus(appointment.Status),
                        doctor = new DoctorBE().Fill(appointment.Doctor)
                    };

                    return Ok(response);
                }

                var today = DateTime.Now.Date;
                response.Result = user.Patient.FirstOrDefault().Appointment
                                         .Where(x => x.PatientId == patient.Id &&
                                                     x.AppointmentDate >= today &&
                                                     x.Status != ConstantHelper.STATUS.INACTIVE)
                    .Select(x => new
                    {
                        id = x.Id,
                        date = x.AppointmentDate,
                        reason = x.Reason,
                        status = ConstantHelper.STATUS.GetStatus(x.Status),
                        doctor = new DoctorBE().Fill(x.Doctor)
                    }).ToList();

                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);

            }
            catch
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "An error has ocurred";
                return new ErrorResult(response, Request);
            }
        }

        [HttpPost]
        [Route("{patientId}/appointments")]
        public async Task<IHttpActionResult> RequestAppointment(int patientId, AddAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    #region Validation

                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                    if (model.AppointmentDate < DateTime.Now)
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Date can't be before than today");

                    if (await context.Appointment.AnyAsync(x => x.AppointmentDate == model.AppointmentDate))
                        return new HttpActionResult(HttpStatusCode.BadRequest, "There is already another appointment there");

                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null || patient.UserId != userId)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    #endregion

                    var appointment = new Appointment
                    {
                        DoctorId = model.DoctorId,
                        PatientId = patient.Id,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        AppointmentDate = model.AppointmentDate,
                        Reason = model.Reason,
                        Status = ConstantHelper.STATUS.REQUESTED
                    };

                    if (appointment.DoctorId is null)
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Missing doctor Id");


                    context.Appointment.Add(appointment);

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    response.Code = HttpStatusCode.Created;
                    response.Message = "success";

                    return Ok(response);
                }
            }
            catch (ArgumentNullException)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "Null request");
            }
            catch (Exception ex)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        [HttpPut]
        [Route("{patientId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> UpdateAppointment(int patientId, int appointmentId, [FromBody]UpdateAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = new ArgumentNullException().Message;
                        return new ErrorResult(response, Request);
                    }

                    if (!ModelState.IsValid)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    if (model.Action != ConstantHelper.AppointmentAction.Confirm &&
                        model.Action != ConstantHelper.AppointmentAction.Reschedule)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Invalid appointment action";
                        return new ErrorResult(response, Request);
                    }

                    var userId = GetUserId();

                    if (userId is null)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null || patient.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Appointment does not exist";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.PatientId != patient.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "What are you doing here";
                        return new ErrorResult(response, Request);
                    }

                    if (model.Action == ConstantHelper.AppointmentAction.Confirm)
                    {
                        if (appointment.Status == ConstantHelper.STATUS.CONFIRMED ||
                            appointment.Status == ConstantHelper.STATUS.INACTIVE)
                        {
                            response.Code = HttpStatusCode.NotFound;
                            response.Status = "error";
                            response.Message = "Appointment does not exist";
                            return new ErrorResult(response, Request);
                        }

                        appointment.Status = ConstantHelper.STATUS.CONFIRMED;
                    }

                    if (model.Action == ConstantHelper.AppointmentAction.Reschedule)
                    {
                        if (appointment.Status == ConstantHelper.STATUS.RESCHEDULE_REQUESTED ||
                            appointment.Status == ConstantHelper.STATUS.INACTIVE)
                        {
                            response.Code = HttpStatusCode.NotFound;
                            response.Status = "error";
                            response.Message = "Appointment does not exist";
                            return new ErrorResult(response, Request);
                        }

                        if (await context.Appointment.AnyAsync(x => x.AppointmentDate == model.AppointmentDate))
                        {
                            response.Code = HttpStatusCode.BadRequest;
                            response.Status = "error";
                            response.Message = "There is already an appointment at that time";
                            return new ErrorResult(response, Request);
                        }

                        appointment.AppointmentDate = model.AppointmentDate.Value;
                        appointment.Status = ConstantHelper.STATUS.RESCHEDULE_REQUESTED;
                    }

                    appointment.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    return Ok(response);
                }

            }
            catch (ArgumentNullException ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpDelete]
        [Route("{patientId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> CancelAppointment(int patientId, int appointmentId)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var userId = GetUserId();

                    if (userId is null)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null || patient.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        if (patient is null || patient.UserId != userId)
                        {
                            response.Code = HttpStatusCode.NotFound;
                            response.Status = "error";
                            response.Message = "Appointment does not exist";
                            return new ErrorResult(response, Request);
                        }

                    if (appointment.PatientId != patient.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "What are you doing here";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED &&
                        appointment.Status != ConstantHelper.STATUS.REQUESTED &&
                        appointment.Status != ConstantHelper.STATUS.RESCHEDULE_REQUESTED)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Appointment does not exist";
                        return new ErrorResult(response, Request);
                    }

                    appointment.UpdatedAt = DateTime.Now;
                    appointment.CanceledAt = DateTime.Now;
                    appointment.Status = ConstantHelper.STATUS.INACTIVE;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    return Ok(response);
                }

            }
            catch (ArgumentNullException ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
            catch (Exception ex)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = ex.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpGet]
        [Route("{patientId}/doctors/{doctorId}/prescriptions")]
        [Route("{patientId}/doctors/{doctorId}/prescriptions/{prescriptionId}")]
        public async Task<IHttpActionResult> GetPrescriptions(int patientId, int doctorId, int? prescriptionId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(doctorId);

                if (patient is null || patient.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Doctor does not exist";
                    return new ErrorResult(response, Request);
                }

                var membership = await context.Membership.FirstOrDefaultAsync(x => x.DoctorId == doctor.Id &&
                                                                                   x.PatientId == patient.Id &&
                                                                                   x.Status == ConstantHelper.STATUS.CONFIRMED);

                if (membership is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Doctor does not attend this patient";
                    return new ErrorResult(response, Request);
                }

                if (prescriptionId.HasValue)
                {
                    var prescription = await context.Prescription.FindAsync(prescriptionId);

                    if (prescription is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Prescription does not exist";
                        return new ErrorResult(response, Request);
                    }

                    if (prescription.Status == ConstantHelper.STATUS.INACTIVE)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Prescription not available";
                        return new ErrorResult(response, Request);
                    }

                    if (prescription.PatientId != patient.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Current user is not part of this prescription";
                        return new ErrorResult(response, Request);
                    }

                    if (prescription.DoctorId != doctor.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Current doctor is not part of this prescription";
                        return new ErrorResult(response, Request);
                    }

                    if (prescription.FinishedAt < DateTime.Now.Date)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Prescription has expired";
                        return new ErrorResult(response, Request);
                    }

                    response.Status = "ok";
                    response.Code = HttpStatusCode.OK;
                    response.Result = new
                    {
                        frequency = prescription.Frencuency,
                        quantity = prescription.Quantity,
                        durationInDays = prescription.DurationInDays,
                        description = prescription.Description,
                        startsAt = prescription.StartedAt,
                        expiresAt = prescription.FinishedAt,
                        type = new PrescriptionTypeBE().Fill(prescription.PrescriptionType)
                    };

                    return Ok(response);
                }

                var today = DateTime.Now.Date;

                response.Result = context.Prescription.Where(x => x.DoctorId == doctor.Id &&
                                                                  x.PatientId == patient.Id &&
                                                                  x.Status != ConstantHelper.STATUS.INACTIVE &&
                                                                  x.FinishedAt >= today)
                        .Select(x => new
                        {
                            frecuency = x.Frencuency,
                            quantity = x.Quantity,
                            durationInDays = x.DurationInDays,
                            description = x.Description,
                            startsdAt = x.StartedAt,
                            expiresAt = x.FinishedAt,
                            type = new PrescriptionTypeBE().Fill(x.PrescriptionType)
                        }).ToList();

                response.Status = "ok";
                response.Code = HttpStatusCode.OK;

                return Ok(response);
            }
            catch
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "An error has ocurred";
                return new ErrorResult(response, Request);
            }
        }

        [HttpDelete]
        [Route("{patientId}/doctors/{doctorId}/prescriptions")]
        public async Task<IHttpActionResult> DeletePrescription(int patientId, int doctorId, int prescriptionId)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var userId = GetUserId();

                if (userId is null)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null || patient.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null)
                {
                    response.Code = HttpStatusCode.BadRequest;
                    response.Status = "error";
                    response.Message = "Doctor does not exist";
                    return new ErrorResult(response, Request);
                }

                var membership = await context.Membership.FirstOrDefaultAsync(x => x.DoctorId == doctor.Id &&
                                                                                   x.PatientId == patientId &&
                                                                                   x.Status == ConstantHelper.STATUS.ACTIVE);

                if (membership is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Doctor is not attending this patient";
                    return new ErrorResult(response, Request);
                }

                var prescription = await context.Prescription.FindAsync(prescriptionId);

                if (prescription is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Prescription does not exist";
                    return new ErrorResult(response, Request);
                }

                if (prescription.DoctorId != doctor.Id)
                {
                    response.Code = HttpStatusCode.Forbidden;
                    response.Status = "error";
                    response.Message = "Wrong doctor";
                    return new ErrorResult(response, Request);
                }

                if (prescription.PatientId != patient.Id)
                {
                    response.Code = HttpStatusCode.Forbidden;
                    response.Status = "error";
                    response.Message = "Wrong patient";
                    return new ErrorResult(response, Request);
                }

                if (prescription.Status != ConstantHelper.STATUS.ACTIVE)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Prescription does not exist";
                    return new ErrorResult(response, Request);
                }

                prescription.Status = ConstantHelper.STATUS.INACTIVE;

                await context.SaveChangesAsync();
                transaction.Complete();

                response.Code = HttpStatusCode.OK;
                response.Status = "ok";

                return Ok(response);
            }
        }

    }
}