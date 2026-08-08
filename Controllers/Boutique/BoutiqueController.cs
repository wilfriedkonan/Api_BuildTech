using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Boutique
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BoutiqueController : ControllerBase
    {
        private readonly BoutiqueService _boutiqueService;
        private readonly ILogger<BoutiqueController> _logger;

        public BoutiqueController(
            BoutiqueService boutiqueService,
            ILogger<BoutiqueController> logger)
        {
            _boutiqueService = boutiqueService;
            _logger = logger;
        }

        /// <summary>
        /// GET ALL - Récupère toutes les boutiques avec pagination
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _boutiqueService.GetAllAsync(page, pageSize);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Boutiques
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération boutiques");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET SEARCH - Recherche de boutiques (nom, type) avec pagination
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? nom,
            [FromQuery] string? type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _boutiqueService.SearchAsync(nom, type, page, pageSize);

                return Ok(new
                {
                    success = result.Success,
                    searchCriteria = new { nom, type },
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Boutiques
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche boutiques");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET TYPES - Liste des types de boutique disponibles (Restaurant, Magasin, ...)
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetTypes()
        {
            try
            {
                var types = await _boutiqueService.GetTypesAsync();
                return Ok(new { success = true, data = types });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types de boutique");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY ID - Récupère une boutique par son ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var boutique = await _boutiqueService.GetByIdAsync(id);

                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable" });

                return Ok(new { success = true, data = boutique });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération boutique {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// POST - Crée une nouvelle boutique (Slug + CodeQR générés automatiquement)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateBoutiqueRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NomBoutique))
                    return BadRequest(new { success = false, message = "Nom de la boutique requis" });

                if (request.IdEntreprise == Guid.Empty)
                    return BadRequest(new { success = false, message = "IdEntreprise requis" });

                if (request.TypeBoutique != "Restaurant" && request.TypeBoutique != "Magasin")
                    return BadRequest(new { success = false, message = "TypeBoutique doit être 'Restaurant' ou 'Magasin'" });

                var boutique = await _boutiqueService.CreateAsync(request);

                if (boutique == null)
                    return StatusCode(500, new { success = false, message = "Erreur création boutique" });

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = boutique.IdBoutique },
                    new
                    {
                        success = true,
                        message = "Boutique créée avec succès",
                        data = boutique
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création boutique");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Met à jour une boutique (Slug/CodeQR/Type non modifiables)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBoutiqueRequest request)
        {
            try
            {
                var boutique = await _boutiqueService.UpdateAsync(id, request);

                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Boutique mise à jour avec succès",
                    data = boutique
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour boutique {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// DELETE - Désactive une boutique (soft delete: EstActif = 0)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _boutiqueService.DeleteAsync(id);

                if (!success)
                    return NotFound(new { success = false, message = "Boutique introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Boutique désactivée avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression boutique {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}