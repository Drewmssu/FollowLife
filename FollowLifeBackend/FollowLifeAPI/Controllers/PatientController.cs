using FollowLifeAPI.Helpers;
using FollowLifeAPI.Models;
using FollowLifeAPI.Models.Patient;
using FollowLifeDataLayer;
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
        [Route("patient/register")]
        public async Task<IHttpActionResult> Register(PRegister model)
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
                    Email = model.Email.ToLower(),
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

        [HttpGet]
        [Route("patient/profile")]
        public async Task<IHttpActionResult> Profile()
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var patient = user.Patient.FirstOrDefault();

                if (patient != null)
                    return Ok(new
                    {
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        phoneNumber = user.PhoneNumber,
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        email = user.Email
                    });

                throw new Exception();
            }
            catch
            {
                return new ErrorResult();
            }
        }

        [HttpPut]
        [Route("patient/profile")]
        public async Task<IHttpActionResult> Profile(PProfile model)
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

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var patient = user.Patient.FirstOrDefault();

                    user.PhoneNumber = model.PhoneNumber;
                    patient.Age = model.Age;
                    patient.Weight = model.Weight;
                    patient.Height = model.Height;
                    patient.BloodType = model.BloodType;
                    patient.Sex = model.Sex;

                    await context.SaveChangesAsync();

                    if (model.ProfileImage != null)
                    {
                        var image = ImageHelper.UploadImage(model.ProfileImage);
                        if (image != null)
                            user.ProfilePicture = image;
                    }

                    await context.SaveChangesAsync();

                    var result = new
                    {
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        phoneNumber = user.PhoneNumber,
                        age = patient.Age,
                        weight = patient.Weight,
                        height = patient.Height,
                        sex = patient.Sex,
                        bloodType = patient.BloodType,
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

        [HttpPut]
        [Route("patient/membership")]
        public async Task<IHttpActionResult> ActivateMembership(ActivateMembership model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA);

                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var patient = user.Patient.FirstOrDefault();
                    var membership = await context.Membership.FirstOrDefaultAsync(x => x.ReferencedEmail == user.Email &&
                                                                                       x.Token == model.Code &&
                                                                                       x.Status == ConstantHelper.STATUS.ACTIVE);

                    if (membership is null)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Membership not found");

                    if (membership.ExpiresAt < DateTime.Now)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Your code has expired");

                    membership.PatientId = patient.Id;
                    membership.Status = ConstantHelper.STATUS.CONFIRMED;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    return Ok(new ErrorResult(ErrorHelper.STATUS_OK));
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

        [HttpGet]
        [Route("patient/doctors")]
        [Route("patient/doctors/{doctorId}")]
        public async Task<IHttpActionResult> GetDoctors(int? doctorId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var patient = user.Patient.FirstOrDefault();

                if (doctorId.HasValue)
                {
                    var doctor = context.Membership.FirstOrDefault(x => x.DoctorId == doctorId &&
                                                                         x.PatientId == patient.Id &&
                                                                         x.Status == ConstantHelper.STATUS.CONFIRMED)?.Doctor;

                    if (doctor is null)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Doctor does not exist");

                    //TODO: Validate if doctor membership is active (up to date on his payments)
                    //TODO: Returns indicator (IMPORTANT)

                    return Ok(new
                    {
                        profileImage = ImageHelper.GetImageURL(doctor.User.ProfilePicture),
                        name = doctor.User.FirstName,
                        lastName = doctor.User.LastName,
                        medicIndentification = doctor.MedicIdentification,
                        address = new AddressBE().Fill(doctor.Address),
                        medicalSpecialities = new MedicalSpecialityBE().Fill(doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality))
                    });
                }

                var result = patient.Membership.Where(x => x.PatientId == patient.Id &&
                                                           x.Status == ConstantHelper.STATUS.CONFIRMED)
                    .Select(x => new
                    {
                        id = x.DoctorId,
                        name = x.Doctor.User.FirstName + x.Doctor.User.LastName,
                        profileImage = ImageHelper.GetImageURL(x.Doctor.User.ProfilePicture)
                    }).ToList();

                return Ok(result);
            }
            catch
            {
                return new ErrorResult();
            }
        }

        [HttpGet]
        [Route("patient/appointments")]
        [Route("patient/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> GetAppointments(int? appointmentId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.PATIENT)
                    return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                var patient = user.Patient.FirstOrDefault();

                if (appointmentId.HasValue)
                {
                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment does not exist");

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED)
                        return new ErrorResult(ErrorHelper.FORBIDDEN, "Appointment not available");

                    if (appointment.PatientId != patient.Id)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED, "Current user is not part of this appointment");

                    if (appointment.AppointmentDate < DateTime.Now)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment has expired");

                    return Ok(new
                    {
                        createdAt = appointment.CreatedAt,
                        appointmentDate = appointment.AppointmentDate,
                        reason = appointment.Reason,
                        patient = new DoctorBE().Fill(appointment.Doctor)
                    });
                }

                var today = DateTime.Now;
                var result = context.Appointment.Where(x => x.PatientId == patient.Id &&
                                                            x.Status == ConstantHelper.STATUS.CONFIRMED &&
                                                            x.AppointmentDate >= today)
                    .Select(x => new
                    {
                        id = x.Id,
                        date = x.AppointmentDate,
                        reason = x.Reason,
                        doctor = new DoctorBE().Fill(x.Doctor)
                    });

                return Ok(result);

            }
            catch
            {
                return new ErrorResult();
            }
        }

        [HttpPost]
        [Route("patient/appointments")]
        public async Task<IHttpActionResult> RequestAppointment(AddAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    #region Validation

                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new ErrorResult(ErrorHelper.INVALID_MODEL_DATA, ModelState.ToListString());

                    if (model.AppointmentDate < DateTime.Now)
                        return new ErrorResult(ErrorHelper.BAD_REQUEST, "Date can't be before than today");

                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    #endregion

                    var patient = user.Patient.FirstOrDefault();

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
                        return new ErrorResult(ErrorHelper.BAD_REQUEST, "Missing doctorId");

                    context.Appointment.Add(appointment);

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    var result = new ErrorResult(ErrorHelper.STATUS_OK, "Appointment added succesfully");

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

        [HttpPut]
        [Route("patient/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> UpdateAppointment(int appointmentId, [FromBody]UpdateAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var patient = user.Patient.FirstOrDefault();

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment does not exist");

                    if (appointment.PatientId != patient.Id)
                        return new ErrorResult(ErrorHelper.FORBIDDEN, "What are you doing here");

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED &&
                        appointment.Status != ConstantHelper.STATUS.REQUESTED)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment does not exist");

                    appointment.UpdatedAt = DateTime.Now;
                    appointment.AppointmentDate = model.AppointmentDate;
                    appointment.Status = ConstantHelper.STATUS.REQUESTED;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    var result = new ErrorResult(ErrorHelper.STATUS_OK, "Appointment updated succesfully");

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

        [HttpDelete]
        [Route("patient/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> CancelAppointment(int appointmentId)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var userId = GetUserId();

                    if (userId is null)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new ErrorResult(ErrorHelper.UNAUTHORIZED);

                    var patient = user.Patient.FirstOrDefault();

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment does not exist");

                    if (appointment.PatientId != patient.Id)
                        return new ErrorResult(ErrorHelper.FORBIDDEN, "What are you doing here");

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED &&
                        appointment.Status != ConstantHelper.STATUS.REQUESTED)
                        return new ErrorResult(ErrorHelper.NOT_FOUND, "Appointment does not exist");

                    appointment.UpdatedAt = DateTime.Now;
                    appointment.CanceledAt = DateTime.Now;
                    appointment.Status = ConstantHelper.STATUS.INACTIVE;

                    await context.SaveChangesAsync();
                    transaction.Complete();

                    var result = new ErrorResult(ErrorHelper.STATUS_OK, "Appointment deleted succesfully");

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

        


    }
}