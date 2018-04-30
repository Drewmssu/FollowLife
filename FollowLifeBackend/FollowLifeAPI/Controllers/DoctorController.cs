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

namespace FollowLifeAPI.Controllers
{
    public class DoctorController : BaseController
    {
        [Route("doctor/login")]
        [HttpPost]
        public async Task<IHttpActionResult> Login([FromBody]Login model)
        {
            try
            {
                #region User
                if (model is null)
                    throw new ArgumentException();

                if (!ModelState.IsValid)
                    return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                #endregion

                #region Validation

                var user = (await context.Doctor.FirstOrDefaultAsync(x => x.User.Email == model.Email.ToLower()))?.User;

                if (user is null)
                    return new HttpActionResult(HttpStatusCode.NotFound, "Invalid identifier");

                if (user.Status == ConstantHelper.STATUS.INACTIVE)
                    return new HttpActionResult(HttpStatusCode.NotFound, "User deleted");

                if (user.Status != ConstantHelper.STATUS.CONFIRMED &&
                    user.Status != ConstantHelper.STATUS.ACTIVE)
                    return new HttpActionResult(HttpStatusCode.NotFound, "User not found");

                #endregion

                #region Login

                if (user.Password != CipherLogic.Cipher(CipherBCAction.Encrypt, CipherBCType.UserPassword, model.Password))
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Invalid password");

                #endregion

                if (user.Role.ShortName != ConstantHelper.ROLE.DOCTOR)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

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
                    user.Id,
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
                return new HttpActionResult(HttpStatusCode.BadRequest, "Null Request");
            }
            catch (Exception ex)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, ex.Message);
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
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var user = await context.User.FindAsync(userId);

                user.SessionToken = null;

                await context.SaveChangesAsync();

                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }
            catch
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "An error has ocurred");
            }
        }

        [HttpPost]
        [Route("doctor/register")]
        public async Task<IHttpActionResult> Register(Register model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                    #region Validation

                    model.Email = model.Email.ToLower();

                    if (await context.User.AnyAsync(x => x.Email == model.Email))
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Email already exists");

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

        [HttpGet]
        [Route("doctor/profile")]
        public async Task<IHttpActionResult> Profile()
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var doctor = user?.Doctor.FirstOrDefault();

                if (doctor != null)
                {
                    response.Result = new
                    {
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        phoneNumber = user.PhoneNumber,
                        profileImage = ImageHelper.GetImageURL(user.ProfilePicture),
                        email = user.Email,
                        medicIdentification = doctor.MedicIdentification,
                        address = new AddressBE().Fill(doctor.Address),
                        medicalSpeciality = new MedicalSpecialityBE().Fill(doctor.DoctorMedicalSpeciality.Select(x => x.MedicalSpeciality)),
                        numberOfPatients = doctor.Membership.Count(x => x.DoctorId == doctor.Id && x.Status == ConstantHelper.STATUS.CONFIRMED)
                    };
                    response.Code = HttpStatusCode.OK;
                    response.Message = "success";

                    return Ok(response);
                }

                throw new Exception();
            }
            catch
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "An error has ocurred");
            }
        }

        [HttpPut]
        [Route("doctor/profile")]
        public async Task<IHttpActionResult> Profile(DProfile model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var doctor = user.Doctor.FirstOrDefault();
                    var address = doctor.Address;

                    user.PhoneNumber = model.PhoneNumber;
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

                    response.Result = new
                    {
                        phoneNumber = model.PhoneNumber,

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
                return new HttpActionResult(HttpStatusCode.BadRequest, "Null request");
            }
            catch (Exception ex)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        [HttpPost]
        [Route("doctor/membership")]
        public async Task<IHttpActionResult> GenerateMembership(GenerateMembership model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var doctor = user.Doctor.FirstOrDefault();

                    model.Email = model.Email.ToLower();

                    var patient = await context.Patient.FirstOrDefaultAsync(x => x.User.Email == model.Email);

                    if (patient is null)
                        return new HttpActionResult(HttpStatusCode.BadRequest, "There is no user with that email");

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
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Email could not be sent, please try again");

                    context.Membership.Add(membership);
                    await context.SaveChangesAsync();

                    transaction.Complete();

                    response.Code = HttpStatusCode.OK;
                    response.Message = "Your code expires at " + membership.ExpiresAt;

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

        [HttpGet]
        [Route("doctor/patients")]
        [Route("doctor/patients/{patientId}")]
        public async Task<IHttpActionResult> GetPatients(int? patientId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var doctor = user.Doctor.FirstOrDefault();

                if (patientId.HasValue)
                {
                    var patient = context.Membership.FirstOrDefault(x => x.PatientId == patientId &&
                                                                         x.DoctorId == doctor.Id &&
                                                                         x.Status == ConstantHelper.STATUS.CONFIRMED)?.Patient;

                    if (patient is null)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Patient does not exist");

                    //TODO: Validate if patient membership is active (up to date on his payments)
                    //TODO: Returns indicator (IMPORTANT)

                    return Ok(new
                    {
                        profileImage = ImageHelper.GetImageURL(patient.User.ProfilePicture),
                        name = patient.User.FirstName,
                        lastName = patient.User.LastName,
                        age = patient.Age,
                        height = patient.Height,
                        weight = patient.Weight,
                        bloodType = patient.BloodType,
                        sex = patient.Sex
                    });
                }

                response.Result = doctor.Membership.Where(x => x.DoctorId == doctor.Id &&
                                                          x.Status == ConstantHelper.STATUS.CONFIRMED)
                    .Select(x => new
                    {
                        id = x.PatientId,
                        name = x.Patient.User.FirstName + x.Patient.User.LastName,
                        profileImage = ImageHelper.GetImageURL(x.Patient.User.ProfilePicture)
                    }).ToList();

                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }
            catch
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "An error has ocurred");
            }
        }

        [HttpGet]
        [Route("doctor/appointments")]
        [Route("doctor/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> GetAppointments(int? appointmentId = null)
        {
            try
            {
                var userId = GetUserId();

                if (userId is null)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var user = await context.User.FindAsync(userId);

                if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                    return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                var doctor = user.Doctor.FirstOrDefault();

                if (appointmentId.HasValue)
                {
                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED)
                        return new HttpActionResult(HttpStatusCode.Forbidden, "Appointment not available");                

                    if (appointment.DoctorId != doctor.Id)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Current user is not part of this appointment");

                    if (appointment.AppointmentDate < DateTime.Now)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Appointment has expired");

                    return Ok(new
                    {
                        createdAt = appointment.CreatedAt,
                        appointmentDate = appointment.AppointmentDate,
                        reason = appointment.Reason,
                        status = ConstantHelper.STATUS.GetStatus(appointment.Status),
                        patient = new PatientBE().Fill(appointment.Patient)
                    });
                }

                var today = DateTime.Now.Date;
                var result = user.Doctor.FirstOrDefault().Appointment
                                        .Where(x => x.DoctorId == doctor.Id &&
                                                    x.Status != ConstantHelper.STATUS.INACTIVE &&
                                                    x.AppointmentDate >= today)
                    .Select(x => new
                    {
                        id = x.Id,
                        date = x.AppointmentDate,
                        reason = x.Reason,
                        patient = new PatientBE().Fill(x.Patient),
                        status = ConstantHelper.STATUS.GetStatus(x.Status)
                    }).ToList();

                return Ok(result);

            }
            catch
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "An error has ocurred");
            }
        }

        [HttpPost]
        [Route("doctor/appointments")]
        public async Task<IHttpActionResult> AddAppointment(AddAppointment model)
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
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Schedule not available");

                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    #endregion

                    var doctor = user.Doctor.FirstOrDefault();

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
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Missing patientId");

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
        [Route("doctor/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> UpdateAppointment(int appointmentId, [FromBody]UpdateAppointment model)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (model is null)
                        throw new ArgumentNullException();

                    if (!ModelState.IsValid)
                        return new HttpActionResult(HttpStatusCode.NoContent, ModelState.ToString());

                    if (model.Action != ConstantHelper.AppointmentAction.Confirm &&
                        model.Action != ConstantHelper.AppointmentAction.Reschedule)
                        return new HttpActionResult(HttpStatusCode.BadRequest, "Invalid appointment action");

                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var doctor = user.Doctor.FirstOrDefault();

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

                    if (appointment.DoctorId != doctor.Id)
                        return new HttpActionResult(HttpStatusCode.Forbidden, "What are you doing here");

                    if (model.Action is ConstantHelper.AppointmentAction.Confirm)
                    {
                        if (appointment.Status == ConstantHelper.STATUS.CONFIRMED ||
                            appointment.Status == ConstantHelper.STATUS.INACTIVE)
                            return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

                        appointment.Status = ConstantHelper.STATUS.CONFIRMED;
                    }

                    if (model.Action is ConstantHelper.AppointmentAction.Reschedule)
                    {
                        if (appointment.Status == ConstantHelper.STATUS.RESCHEDULE_REQUESTED ||
                            appointment.Status == ConstantHelper.STATUS.INACTIVE)
                            return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

                        if (await context.Appointment.AnyAsync(x => x.AppointmentDate == model.AppointmentDate))
                            return new HttpActionResult(HttpStatusCode.BadRequest, "There is already an appointment at that time");

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
            catch (ArgumentNullException)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "Null request");
            }
            catch (Exception ex)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        [HttpDelete]
        [Route("doctor/appointments/{appointmentId}")]
        public async Task<IHttpActionResult> CancelAppointment(int appointmentId)
        {
            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var userId = GetUserId();

                    if (userId is null)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var user = await context.User.FindAsync(userId);

                    if (user.RoleId != ConstantHelper.ROLE.ID.DOCTOR)
                        return new HttpActionResult(HttpStatusCode.Unauthorized, "Unauthorized");

                    var doctor = user.Doctor.FirstOrDefault();

                    var appointment = await context.Appointment.FindAsync(appointmentId);

                    if (appointment is null)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

                    if (appointment.DoctorId != doctor.Id)
                        return new HttpActionResult(HttpStatusCode.Forbidden, "What are you doing here");

                    if (appointment.Status != ConstantHelper.STATUS.CONFIRMED &&
                        appointment.Status != ConstantHelper.STATUS.REQUESTED &&
                        appointment.Status != ConstantHelper.STATUS.RESCHEDULE_REQUESTED)
                        return new HttpActionResult(HttpStatusCode.NotFound, "Appointment does not exist");

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
            catch (ArgumentNullException)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, "Null request");
            }
            catch (Exception ex)
            {
                return new HttpActionResult(HttpStatusCode.BadRequest, ex.Message);
            }
        }

    }
}
