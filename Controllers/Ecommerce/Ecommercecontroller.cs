using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Ecommerce
{
    [ApiController]
    [Route("api/commandes")]
    [Authorize]
    public class Ecommercecontroller : ControllerBase
    {
        private readonly Ecommerceservice _commandeService;
        private readonly ILogger<Ecommercecontroller> _logger;

        public Ecommercecontroller(
            Ecommerceservice commandeService,
            ILogger<Ecommercecontroller> logger)
        {
            _commandeService = commandeService;
            _logger = logger;
        }

        /// <summary>
        /// GET - Liste des commandes (POS). Filtrer avec ?etat=EnAttente pour la file d'attente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? idBoutique,
            [FromQuery] string? etat,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _commandeService.GetAsync(idBoutique, etat, page, pageSize);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Commandes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération commandes");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Détail d'une commande avec ses lignes
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var commande = await _commandeService.GetByIdAsync(id);

                if (commande == null)
                    return NotFound(new { success = false, message = "Commande introuvable" });

                return Ok(new { success = true, data = commande });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération commande {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Changer l'état d'une commande (Confirmee, EnPreparation, Prete, Livree, Annulee)
        /// </summary>
        [HttpPut("{id}/etat")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> UpdateEtat(Guid id, [FromBody] UpdateEtatRequest request)
        {
            try
            {
                var success = await _commandeService.UpdateEtatAsync(id, request.Etat);

                if (!success)
                    return BadRequest(new { success = false, message = "Commande introuvable ou état invalide" });

                return Ok(new { success = true, message = $"Commande mise à jour: {request.Etat}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur changement état commande {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }

    public class UpdateEtatRequest
    {
        public string Etat { get; set; } = "";
    }
}




