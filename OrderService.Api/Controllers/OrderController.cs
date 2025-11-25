using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Application.Services;

namespace OrderService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OrderAppService _service;

        public OrderController(OrderAppService service)
        {
            _service = service;
        }

        // ================================
        // POST /api/order  (Create Order)
        // ================================
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateOrderDto dto,
            [FromHeader(Name = "Idempotency-Key")] string idemKey)
        {
            // 1) Idempotency-Key zorunlu
            if (string.IsNullOrWhiteSpace(idemKey))
                return BadRequest("Idempotency-Key header is required.");

            // 2) GUID format kontrolü
            if (!Guid.TryParse(idemKey, out var parsedGuid))
                return BadRequest("Idempotency-Key must be a valid GUID.");

            // 3) Create order + idempotency
            var id = await _service.CreateOrderAsync(dto, parsedGuid);

            return Ok(new { OrderId = id });
        }

        // ================================
        // GET /api/order/{id}
        // ================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _service.GetByIdAsync(id);

            if (order == null)
                return NotFound();

            return Ok(order);
        }
    }
}
