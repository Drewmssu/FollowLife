using FollowLifeAPI.Helpers;
using FollowLifeAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Data.Entity;
using System.Net;
using FollowLifeLogic;
using FollowLifeAPI.BE;
using System.Transactions;
using FollowLifeService.MailJet;
using FollowLifeAPI.Extensions;
using FollowLifeAPI.Models.Doctor;
using FollowLifeAPI.Models.Appointment;
using FollowLifeAPI.DataLayer;
using FollowLifeAPI.Models.Prescription;

namespace FollowLifeAPI.Controllers
{
    [RoutePrefix("api/v1/doctors")]
    public class DoctorController : BaseController
    {
        [Route("login")]
        [HttpPost]
        public async Task<IHttpActionResult> Login([FromBody]Login model)
        {
            try
            {
                #region User
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

                #endregion

                #region Validation

                var user = (await context.Doctor.FirstOrDefaultAsync(x => x.User.Email == model.Email.ToLower()))?.User;

                if (user is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Not Found, invalid identifier";
                    return new ErrorResult(response, Request);
                }

                if (user.Status == ConstantHelper.STATUS.INACTIVE)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Not Found, user deleted";
                    return new ErrorResult(response, Request);
                }

                if (user.Status != ConstantHelper.STATUS.CONFIRMED &&
                    user.Status != ConstantHelper.STATUS.ACTIVE)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Not Found, user not found";
                    return new ErrorResult(response, Request);
                }

                #endregion

                #region Login

                if (user.Password != CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password))
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unahutorized, invalid password";
                    return new ErrorResult(response, Request);
                }

                #endregion

                if (user.Role.ShortName != ConstantHelper.ROLE.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unahutorized, not a doctor";
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
                    user.Doctor.FirstOrDefault(x => x.UserId == user.Id).Id,
                    user.SessionToken,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    ProfileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                    user.PhoneNumber
                };
                response.Code = HttpStatusCode.OK;
                response.Status = "ok";

                return Ok(response);
            }
            catch (ArgumentNullException)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "Null Request";
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
        public async Task<IHttpActionResult> Register([FromBody]Register model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                    {
                        response.Code = HttpStatusCode.NoContent;
                        response.Status = "error";
                        response.Message = ModelState.ToSafeString();
                        return new ErrorResult(response, Request);
                    }

                    #region Validation

                    model.Email = model.Email.ToLower();

                    if (await context.User.AnyAsync(x => x.Email == model.Email))
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Email already exists";
                        return new ErrorResult(response, Request);
                    }

                    #endregion

                    var user = new User
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email,
                        Password = CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password),
                        RoleId = ConstantHelper.ROLE.ID.DOCTOR,
                        Status = ConstantHelper.STATUS.ACTIVE,
                        CreatedAt = DateTime.Now,
                        LastIPConnection = HttpContext.Current.Request.UserHostAddress
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
                    transaction.Complete();

                    response.Status = "ok";
                    response.Code = HttpStatusCode.Created;

                    return Ok(response);
                }
            }
            catch (ArgumentNullException)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "Null Request";
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
        [Route("{doctorId}")]
        public async Task<IHttpActionResult> Profile(int doctorId)
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

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null || doctor.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                response.Result = new
                {
                    user = new UserBE().Fill(doctor.User),
                    medicIdentification = doctor.MedicIdentification,
                    address = new AddressBE().Fill(doctor.Address),
                    medicalSpeciality = new MedicalSpecialityBE().Fill(doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality)),
                    numberOfPatients = doctor.Membership.Count(x => x.DoctorId == doctor.Id && x.Status == ConstantHelper.STATUS.CONFIRMED)
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
        [Route("{doctorId}")]
        public async Task<IHttpActionResult> Profile(int doctorId, [FromBody]DProfile model)
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
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = ModelState.ToString();
                        return new ErrorResult(response, Request);
                    }

                    var address = doctor.Address;

                    if (!string.IsNullOrEmpty(model.PhoneNumber))
                        user.PhoneNumber = model.PhoneNumber;

                    if (!string.IsNullOrEmpty(model.MedicalIdentification))
                        doctor.MedicIdentification = model.MedicalIdentification;

                    user.UpdatedOn = DateTime.Now;
                    doctor.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    if (model.ProfilePicture != null)
                    {
                        var image = ImageHelper.UploadImage(model.ProfilePicture);
                        if (image != null)
                            user.ProfilePicture = image;
                    }

                    await context.SaveChangesAsync();

                    if (model.District.HasValue)
                    {
                        if (address is null)
                        {
                            address = new Address
                            {
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now,
                                Status = ConstantHelper.STATUS.ACTIVE
                            };

                            doctor.AddressId = address.Id;
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

                    if (model.MedicalSpecialities != null)
                    {
                        if (model.MedicalSpecialities.Length > 0)
                        {
                            foreach (var ms in model.MedicalSpecialities)
                            {
                                context.DoctorMedicalSpeciality.Add(new DoctorMedicalSpeciality
                                {
                                    DoctorId = doctor.Id,
                                    MedicalSpecialityId = ms
                                });
                            }

                            await context.SaveChangesAsync();
                        }
                    }

                    response.Result = new
                    {
                        phoneNumber = user.PhoneNumber,
                        medicIdentification = doctor.MedicIdentification,

                        districtId = address?.DistrictId,
                        street = address?.Street,
                        complement = address?.Complement,
                        number = address?.Number,
                        neighborhood = address.Neighborhood,

                        medicalSpecialities = doctor.DoctorMedicalSpeciality.Select(x => x.Id)
                    };
                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    transaction.Complete();

                    return Ok(response);
                }
            }
            catch (ArgumentNullException)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = "Null request";
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

        [HttpPost]
        [Route("{doctorId}/membership")]
        public async Task<IHttpActionResult> GenerateMembership(int doctorId, [FromBody]GenerateMembership model)
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
                        response.Code = HttpStatusCode.BadRequest;
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    model.Email = model.Email.ToLower();

                    var patient = await context.Patient.FirstOrDefaultAsync(x => x.User.Email == model.Email);

                    if (patient is null)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "There is no user with that email";
                        return new ErrorResult(response, Request);
                    }

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
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Email could not be sent, please try again";
                        return new ErrorResult(response, Request);
                    }

                    context.Membership.Add(membership);
                    await context.SaveChangesAsync();

                    transaction.Complete();

                    response.Code = HttpStatusCode.OK;
                    response.Status = "ok";
                    response.Message = "Your code expires at " + membership.ExpiresAt;

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
        [Route("{doctorId}/patients")]
        [Route("{doctorId}/patients/{patientId}")]
        public async Task<IHttpActionResult> GetPatients(int doctorId, int? patientId = null)
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

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null || doctor.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                if (patientId.HasValue)
                {
                    var patient = context.Membership.FirstOrDefault(x => x.PatientId == patientId &&
                                                                         x.DoctorId == doctor.Id &&
                                                                         x.Status == ConstantHelper.STATUS.CONFIRMED)?.Patient;

                    if (patient is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Patient does not exist";
                        return new ErrorResult(response, Request);
                    }

                    //TODO: Validate if patient membership is active (up to date on his payments)
                    //TODO: Returns indicator (IMPORTANT)

                    response.Status = "ok";
                    response.Code = HttpStatusCode.OK;
                    response.Result = new
                    {
                        profileImage = ImageHelper.GetImageURL(patient.User.ProfilePicture),
                        name = patient.User.FirstName,
                        lastName = patient.User.LastName,
                        age = patient.Age,
                        height = patient.Height,
                        weight = patient.Weight,
                        bloodType = patient.BloodType,
                        sex = patient.Sex,
                        email = patient.User.Email,
                        phoneNumber = patient.User.PhoneNumber
                    };

                    return Ok(response);
                }

                response.Result = doctor.Membership.Where(x => x.DoctorId == doctor.Id &&
                                                          x.Status == ConstantHelper.STATUS.CONFIRMED)
                    .Select(x => new
                    {
                        id = x.PatientId,
                        name = x.Patient.User.FirstName + " " + x.Patient.User.LastName,
                        profileImage = ImageHelper.GetImageURL(x.Patient.User.ProfilePicture)
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
        [Route("{doctorId}/appointments")]
        [Route("{doctorId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> GetAppointments(int doctorId, int? appointmentId = null)
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

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null || doctor.UserId != userId)
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

                    if (appointment.DoctorId != doctor.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Current user is not part of this appointment";
                        return new ErrorResult(response, Request);
                    }

                    if (appointment.AppointmentDate < DateTime.Now.Date)
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
                        appointmentDate = appointment.AppointmentDate,
                        patient = new PatientBE().Fill(appointment.Patient),
                        reason = appointment.Reason,
                        createdAt = appointment.CreatedAt,
                        status = ConstantHelper.STATUS.GetStatus(appointment.Status)
                    };

                    return Ok(response);
                }

                var today = DateTime.Now.Date;
                response.Result = user.Doctor.FirstOrDefault().Appointment
                                        .Where(x => x.DoctorId == doctor.Id &&
                                                    x.Status != ConstantHelper.STATUS.INACTIVE &&
                                                    x.AppointmentDate >= today)
                    .Select(x => new
                    {
                        id = x.Id,
                        appointmentDate = x.AppointmentDate,
                        reason = x.Reason,
                        patient = new PatientBE().Fill(x.Patient),
                        status = ConstantHelper.STATUS.GetStatus(x.Status)
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

        [HttpPost]
        [Route("{doctorId}/appointments")]
        public async Task<IHttpActionResult> AddAppointment(int doctorId, [FromBody]AddAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    #region Validation

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

                    if (model.AppointmentDate < DateTime.Now)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Date can't be before than today";
                        return new ErrorResult(response, Request);
                    }

                    if (await context.Appointment.AnyAsync(x => x.AppointmentDate == model.AppointmentDate))
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Schedule not available";
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    #endregion

                    var appointment = new Appointment
                    {
                        DoctorId = doctor.Id,
                        PatientId = model.PatientId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        AppointmentDate = model.AppointmentDate,
                        Reason = model.Reason,
                        Status = ConstantHelper.STATUS.CONFIRMED
                    };

                    if (appointment.PatientId is null)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Missing patientId";
                        return new ErrorResult(response, Request);
                    }

                    context.Appointment.Add(appointment);

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    response.Code = HttpStatusCode.Created;
                    response.Status = "ok";

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
        [Route("{doctorId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> UpdateAppointment(int doctorId, int appointmentId, [FromBody]UpdateAppointment model)
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
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

                    if (appointment.DoctorId != doctor.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "What are you doing here";
                        return new ErrorResult(response, Request);
                    }

                    if (model.Action is ConstantHelper.AppointmentAction.Confirm)
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

                    if (model.Action is ConstantHelper.AppointmentAction.Reschedule)
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
                    response.Status = "ok";
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
        [Route("{doctorId}/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> CancelAppointment(int doctorId, int appointmentId)
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
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

                    if (appointment.DoctorId != doctor.Id)
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
                    response.Status = "ok";
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
        [Route("{doctorId}/patients/{patientId}/prescriptions")]
        [Route("{doctorId}/patients/{patientId}/prescriptions/{prescriptionId}")]
        public async Task<IHttpActionResult> GetPrescriptions(int doctorId, int patientId, int? prescriptionId = null)
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

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null || doctor.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Patient does not exist";
                    return new ErrorResult(response, Request);
                }

                var membership = await context.Membership.FirstOrDefaultAsync(x => x.DoctorId == doctor.Id &&
                                                                                   x.PatientId == patient.Id &&
                                                                                   x.Status == ConstantHelper.STATUS.CONFIRMED);

                if (membership is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Patient is not attended by this doctor";
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

                    if (prescription.DoctorId != doctor.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Current user is not part of this prescription";
                        return new ErrorResult(response, Request);
                    }

                    if (prescription.PatientId != patient.Id)
                    {
                        response.Code = HttpStatusCode.Forbidden;
                        response.Status = "error";
                        response.Message = "Current patient is not part of this prescription";
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
                        type = new PrescriptionTypeBE().Fill(prescription.PrescriptionType),
                        status = ConstantHelper.STATUS.GetStatus(prescription.Status)
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
                            type = x.PrescriptionType.Name
                        }).ToList();

                response.Status = "ok";
                response.Code = HttpStatusCode.OK;

                return Ok(response);
            }
            catch (Exception e)
            {
                response.Code = HttpStatusCode.BadRequest;
                response.Status = "error";
                response.Message = e.Message;
                return new ErrorResult(response, Request);
            }
        }

        [HttpPost]
        [Route("{doctorId}/patients/{patientId}/prescriptions")]
        public async Task<IHttpActionResult> AddPrescription(int doctorId, int patientId, [FromBody]AddPrescription model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    #region Validation

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

                    if (model.StartedAt <= DateTime.Now)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Date can't be before than today";
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var doctor = await context.Doctor.FindAsync(doctorId);

                    if (doctor is null || doctor.UserId != userId)
                    {
                        response.Code = HttpStatusCode.Unauthorized;
                        response.Status = "error";
                        response.Message = "Unauthorized";
                        return new ErrorResult(response, Request);
                    }

                    var patient = await context.Patient.FindAsync(patientId);

                    if (patient is null)
                    {
                        response.Code = HttpStatusCode.BadRequest;
                        response.Status = "error";
                        response.Message = "Patient does not exist";
                        return new ErrorResult(response, Request);
                    }

                    var membership = await context.Membership.FirstOrDefaultAsync(x => x.DoctorId == doctor.Id &&
                                                                                       x.PatientId == patientId &&
                                                                                       x.Status == ConstantHelper.STATUS.CONFIRMED);

                    if (membership is null)
                    {
                        response.Code = HttpStatusCode.NotFound;
                        response.Status = "error";
                        response.Message = "Patient is not attended by this doctor";
                        return new ErrorResult(response, Request);
                    }

                    #endregion

                    var prescription = new Prescription
                    {
                        DoctorId = doctor.Id,
                        PatientId = patientId,
                        PrescriptionTypeId = model.PrescriptionTypeId,
                        Frencuency = model.Frecuency,
                        Quantity = model.Quantity,
                        DurationInDays = model.DurationInDays,
                        Description = model.Description,
                        StartedAt = model.StartedAt,
                        FinishedAt = model.StartedAt.Date.AddDays(model.DurationInDays.Value),
                        CreatedAt = DateTime.Now,
                        Status = ConstantHelper.STATUS.ACTIVE,
                    };

                    context.Prescription.Add(prescription);
                    await context.SaveChangesAsync();

                    transaction.Complete();

                    response.Status = "ok";
                    response.Code = HttpStatusCode.Created;

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

        [HttpDelete]
        [Route("{doctorId}/patients/{patientId}/prescriptions")]
        public async Task<IHttpActionResult> DeletePrescription(int doctorId, int patientId, int prescriptionId)
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

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var doctor = await context.Doctor.FindAsync(doctorId);

                if (doctor is null || doctor.UserId != userId)
                {
                    response.Code = HttpStatusCode.Unauthorized;
                    response.Status = "error";
                    response.Message = "Unauthorized";
                    return new ErrorResult(response, Request);
                }

                var patient = await context.Patient.FindAsync(patientId);

                if (patient is null)
                {
                    response.Code = HttpStatusCode.BadRequest;
                    response.Status = "error";
                    response.Message = "Patient does not exist";
                    return new ErrorResult(response, Request);
                }

                var membership = await context.Membership.FirstOrDefaultAsync(x => x.DoctorId == doctor.Id &&
                                                                                   x.PatientId == patientId &&
                                                                                   x.Status == ConstantHelper.STATUS.ACTIVE);

                if (membership is null)
                {
                    response.Code = HttpStatusCode.NotFound;
                    response.Status = "error";
                    response.Message = "Patient is not attended by this doctor";
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
