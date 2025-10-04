using ECommerce.Core.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public OrdersController(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public record CreateOrderItem(Guid ProductId, int Quantity);
    public record CreateOrderRequest(List<CreateOrderItem> Items);

    [HttpPost]
    public async Task<ActionResult<Order>> Create(CreateOrderRequest request)
    {
        var userName = User.Identity?.Name ?? string.Empty;
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return Unauthorized();

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        if (products.Count != productIds.Count) return BadRequest("One or more products not found.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrderDateUtc = DateTime.UtcNow,
        };

        decimal total = 0m;
        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            if (product.StockQuantity < item.Quantity)
            {
                return BadRequest($"Insufficient stock for {product.Name}");
            }

            product.StockQuantity -= item.Quantity;
            var orderItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            };
            order.Items.Add(orderItem);
            total += product.Price * item.Quantity;
        }
        order.Total = total;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> ListMine()
    {
        var userName = User.Identity?.Name ?? string.Empty;
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return Unauthorized();

        var orders = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.OrderDateUtc)
            .AsNoTracking()
            .ToListAsync();
        return orders;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetById(Guid id)
    {
        var userName = User.Identity?.Name ?? string.Empty;
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
        return order is null ? NotFound() : order;
    }
}
