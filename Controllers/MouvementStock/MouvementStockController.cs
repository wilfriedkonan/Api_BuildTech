using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.MouvementStock
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MouvementStockController : ControllerBase
    {
        private readonly MouvementStockService _mouvementService;
        private readonly ILogger<MouvementStockController> _logger;

        public MouvementStockController(
            MouvementStockService mouvementService,
            ILogger<MouvementStockController> logger)
        {
            _mouvementService = mouvementService;
            _logger = logger;
        }

        /// <summary>
        /// Liste paginee des mouvements.
        /// GET api/MouvementStock?page=1&pageSize=20&dateDebut=2026-11-01&dateFin=2026-11-06&typeMouvement=Sortie
        /// Reponse : { success, total, totalEntrees, ..., mouvements[], pagination{ currentPage, pageSize, totalPages, totalRecords, hasPrevious, hasNext } }
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] DateTime? dateDebut = null,
            [FromQuery] DateTime? dateFin = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? typeMouvement = null)
        {
            try
            {
                if (dateDebut.HasValue && dateFin.HasValue && dateDebut.Value > dateFin.Value)
                    return BadRequest(new { success = false, message = "La date de début doit être antérieure à la date de fin" });

                // Bornes de pagination (le service les applique aussi : pageSize 1..100)
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var result = await _mouvementService.GetAllAsync(dateDebut, dateFin, page, pageSize, typeMouvement?.Trim());

                if (!result.Success)
                    return StatusCode(500, new { success = false, message = "Erreur serveur" });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération mouvements stock");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var mouvement = await _mouvementService.GetByIdAsync(id);
                if (mouvement == null)
                    return NotFound(new { success = false, message = "Mouvement stock introuvable" });

                return Ok(new { success = true, data = mouvement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération mouvement stock {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize()]
        public async Task<IActionResult> Create([FromBody] CreateMouvementStockRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TypeMouvement))
                    return BadRequest(new { success = false, message = "Type mouvement requis" });

                if (request.TypeMouvement != "Entree" && request.TypeMouvement != "Sortie")
                    return BadRequest(new { success = false, message = "Type mouvement doit être 'Entree' ou 'Sortie'" });

                var mouvement = await _mouvementService.CreateAsync(request);
                if (mouvement == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = mouvement.Id },
                    new { success = true, data = mouvement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création mouvement stock");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMouvementStockRequest request)
        {
            try
            {
                var mouvement = await _mouvementService.UpdateAsync(id, request);
                if (mouvement == null)
                    return NotFound(new { success = false, message = "Mouvement stock introuvable" });

                return Ok(new { success = true, data = mouvement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour mouvement stock {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _mouvementService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Mouvement stock introuvable" });

                return Ok(new { success = true, message = "Mouvement stock supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression mouvement stock {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}