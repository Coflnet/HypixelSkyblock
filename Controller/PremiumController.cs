using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for premium users
    /// </summary>
    [ApiController]
    [Route("api")]
    public class PremiumController : ControllerBase
    {
        /// <summary>
        /// When the flipper will update next
        /// </summary>
        /// <returns></returns>
        [Route("flipper/updateTime")]
        [HttpGet]
        public ActionResult<DateTime> UpdateTime()
        {
            return Ok(hypixel.Updater.LastPull);
        }
    }
}

