using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Table
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TableController : ControllerBase
    {
        private readonly TableService _tableService;
        private readonly ILogger<TableController> _logger;

        public TableController(
            TableService tableService,
            ILogger<TableController> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _tableService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération tables");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("disponibles")]
        public async Task<IActionResult> GetDisponibles()
        {
            try
            {
                var result = await _tableService.GetDisponiblesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération tables disponibles");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var table = await _tableService.GetByIdAsync(id);
                if (table == null)
                    return NotFound(new { success = false, message = "Table introuvable" });

                return Ok(new { success = true, data = table });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération table {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateTableRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var table = await _tableService.CreateAsync(request);
                if (table == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = table.Id },
                    new { success = true, data = table });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création table");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTableRequest request)
        {
            try
            {
                var table = await _tableService.UpdateAsync(id, request);
                if (table == null)
                    return NotFound(new { success = false, message = "Table introuvable" });

                return Ok(new { success = true, data = table });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour table {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}/affecter")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Serveur")]
        public async Task<IActionResult> AffecterServeur(Guid id, [FromBody] AffecterServeurRequest request)
        {
            try
            {
                var table = await _tableService.AffecterServeurAsync(id, request);
                if (table == null)
                    return NotFound(new { success = false, message = "Table introuvable" });

                return Ok(new { success = true, data = table });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur affectation serveur table {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}/liberer")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Serveur")]
        public async Task<IActionResult> LibererTable(Guid id)
        {
            try
            {
                var table = await _tableService.LibererTableAsync(id);
                if (table == null)
                    return NotFound(new { success = false, message = "Table introuvable" });

                return Ok(new { success = true, data = table });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur libération table {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _tableService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Table introuvable" });

                return Ok(new { success = true, message = "Table supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression table {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}