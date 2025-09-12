using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BIDashboardBackend.Controllers
{
    public class BaseController: ControllerBase
    {
        protected long UserId
        {
            get
            {
                int userId;
                int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
                return userId;
            }
        }
    }
}
