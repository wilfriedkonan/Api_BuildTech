using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Fournisseurs
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FournisseursController : ControllerBase
    {
        private readonly FournisseursService _fournisseursService;
        private readonly ILogger<FournisseursController> _logger;

        public FournisseursController(
            FournisseursService fournisseursService,
            ILogger<FournisseursController> logger)
        {
            _fournisseursService = fournisseursService;
            _logger = logger;
        }

        /// <summary>
        /// GET ALL - Récupère tous les fournisseurs avec pagination
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _fournisseursService.GetAllAsync(page, pageSize);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalActifs = result.TotalActifs,
                    totalInactifs = result.TotalInactifs,
                    pagination = result.Pagination,
                    data = result.Fournisseurs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération fournisseurs");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET SEARCH - Recherche de fournisseurs avec pagination
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? nom,
            [FromQuery] string? contact,  // ✅ CORRIGÉ : "contact" au lieu de "telephone"
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _fournisseursService.SearchAsync(nom, contact, page, pageSize);

                return Ok(new
                {
                    success = result.Success,
                    searchCriteria = new { nom, contact },
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Fournisseurs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche fournisseurs");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY ID - Récupère un fournisseur par son ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var fournisseur = await _fournisseursService.GetByIdAsync(id);

                if (fournisseur == null)
                    return NotFound(new { success = false, message = "Fournisseur introuvable" });

                return Ok(new { success = true, data = fournisseur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération fournisseur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// POST - Crée un nouveau fournisseur
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateFournisseurRequest request)
        {
            try
            {
                // Validations
                if (string.IsNullOrEmpty(request.Nom))
                    return BadRequest(new { success = false, message = "Nom du fournisseur requis" });

                // Créer le fournisseur
                var fournisseur = await _fournisseursService.CreateAsync(request);

                if (fournisseur == null)
                    return StatusCode(500, new { success = false, message = "Erreur création fournisseur" });

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = fournisseur.Id },
                    new
                    {
                        success = true,
                        message = "Fournisseur créé avec succès",
                        data = fournisseur
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création fournisseur");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Met à jour un fournisseur
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFournisseurRequest request)
        {
            try
            {
                var fournisseur = await _fournisseursService.UpdateAsync(id, request);

                if (fournisseur == null)
                    return NotFound(new { success = false, message = "Fournisseur introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Fournisseur mis à jour avec succès",
                    data = fournisseur
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour fournisseur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// DELETE - Supprime un fournisseur (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _fournisseursService.DeleteAsync(id);

                if (!success)
                    return NotFound(new { success = false, message = "Fournisseur introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Fournisseur supprimé avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression fournisseur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}