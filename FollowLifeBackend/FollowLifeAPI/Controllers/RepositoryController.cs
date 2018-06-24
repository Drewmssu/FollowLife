using FollowLifeAPI.Models.Repository;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace FollowLifeAPI.Controllers
{
    [RoutePrefix("api/v1")]
    public class RepositoryController : BaseController
    {
        [HttpGet]
        [Route("repository/medicalSpecialities")]
        [Route("repository/medicalSpecialities/{medicalSpecialityId}")]
        public async Task<IHttpActionResult> MedicalSpecialities(int? medicalSpecialityId = null)
        {
            if (medicalSpecialityId.HasValue)
            {
                var element = await context.MedicalSpeciality.FindAsync(medicalSpecialityId);

                if (element != null)
                    response.Result = new Element
                    {
                        Code = element.Id,
                        Text = element.Name
                    };

                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }

            var result = new ConcurrentBag<Element>();
            var collection = await context.MedicalSpeciality.ToListAsync();

            Parallel.ForEach(collection, element =>
            {
                result.Add(new Element
                {
                    Code = element.Id,
                    Text = element.Name
                });
            });

            response.Code = HttpStatusCode.OK;
            response.Message = "success";
            response.Result = result;
            return Ok(response);
        }

        [HttpGet]
        [Route("repository/prescriptionTypes")]
        [Route("repository/prescriptionTypes/{prescriptionTypeId}")]
        public async Task<IHttpActionResult> PrescriptionTypes(int? itemTypeId = null)
        {
            if (itemTypeId.HasValue)
            {
                var element = await context.PrescriptionType.FindAsync(itemTypeId);

                if (element != null)
                    response.Result = new Element
                    {
                        Code = element.Id,
                        Text = element.Name
                    };
                response.Code = HttpStatusCode.OK;
                response.Message = "success";
                return Ok(response);
            }

            var result = new ConcurrentBag<Element>();
            var collection = await context.PrescriptionType.ToListAsync();

            Parallel.ForEach(collection, element =>
            {
                result.Add(new Element
                {
                    Code = element.Id,
                    Text = element.Name
                });
            });
            response.Code = HttpStatusCode.OK;
            response.Message = "success";
            response.Result = result;

            return Ok(response);
        }

        [HttpGet]
        [Route("repository/districts")]
        [Route("repository/districts/{districtId}")]
        public async Task<IHttpActionResult> Districts(int? districtId = null)
        {
            if (districtId.HasValue)
            {
                var element = await context.District.FindAsync(districtId);

                if (element != null)
                    response.Result = new Element
                    {
                        Code = element.Id,
                        Text = element.Name
                    };
                response.Code = HttpStatusCode.OK;
                response.Message = "success";

                return Ok(response);
            }

            var result = new ConcurrentBag<Element>();
            var collection = await context.District.ToListAsync();

            Parallel.ForEach(collection, element =>
            {
                result.Add(new Element
                {
                    Code = element.Id,
                    Text = element.Name
                });
            });

            response.Code = HttpStatusCode.OK;
            response.Message = "success";
            response.Result = result;
            
            return Ok(response);
        }
    }
}