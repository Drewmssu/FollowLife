using FollowLifeAPI.Models.Repository;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace FollowLifeAPI.Controllers
{
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
                    return Ok(new Element
                    {
                        Code = element.Id,
                        Text = element.Name
                    });
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

            return Ok(result);
        }

        [HttpGet]
        [Route("repository/itemTypes")]
        [Route("repository/itemTypes/{itemTypeId}")]
        public async Task<IHttpActionResult> ItemTypes(int? itemTypeId = null)
        {
            if (itemTypeId.HasValue)
            {
                var element = await context.ItemType.FindAsync(itemTypeId);

                if (element != null)
                    return Ok(new Element
                    {
                        Code = element.Id,
                        Text = element.Name
                    });
            }

            var result = new ConcurrentBag<Element>();
            var collection = await context.ItemType.ToListAsync();

            Parallel.ForEach(collection, element =>
            {
                result.Add(new Element
                {
                    Code = element.Id,
                    Text = element.Name
                });
            });

            return Ok(result);
        }
    }
}