using Linkora.Models;
using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Linkora.Controllers
{
    public class ShipmentsController(IShipmentRepository shipments, IUserRepository users) : Controller
    {
        /// <summary>Seller's delivery requests: accept or decline, pay the delivery, download the label.</summary>
        [Authorize]
        public async Task<IActionResult> Seller()
        {
            var userId = User.GetUserId();
            if (await users.IsBannedAsync(userId)) return Forbid();

            var user = await users.GetByIdAsync(userId);
            return View(new SellerShipmentsViewModel(await shipments.GetSellerRequestsAsync(userId), user?.Phone, user?.Email));
        }
    }
}
