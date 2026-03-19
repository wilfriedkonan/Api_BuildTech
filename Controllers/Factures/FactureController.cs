using Api_BuildTech.Controllers.Factures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Factures
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FactureController : ControllerBase
    {
        private readonly FactureService _factureService;
        private readonly ILogger<FactureController> _logger;

        public FactureController(
            FactureService factureService,
            ILogger<FactureController> logger)
        {
            _factureService = factureService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin)
        {
            try
            {
                var result = await _factureService.GetAllAsync(dateDebut, dateFin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("enattente")]
        public async Task<IActionResult> GetEnAttente()
        {
            try
            {
                var result = await _factureService.GetEnAttenteAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures en attente");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("nonsoldees")]
        public async Task<IActionResult> GetNonSoldees()
        {
            try
            {
                var result = await _factureService.GetNonSoldeesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures non soldées");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var facture = await _factureService.GetByIdAsync(id);
                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new { success = true, data = facture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Caissier,Serveur")]
        public async Task<IActionResult> Create([FromBody] CreateFactureRequest request)
        {
            try
            {
                var facture = await _factureService.CreateAsync(request);
                if (facture == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = facture.Id },
                    new { success = true, data = facture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création facture");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Caissier")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFactureRequest request)
        {
            try
            {
                var facture = await _factureService.UpdateAsync(id, request);
                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new { success = true, data = facture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}/solder")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Caissier")]
        public async Task<IActionResult> SolderFacture(Guid id, [FromBody] SolderFactureRequest request)
        {
            try
            {
                var facture = await _factureService.SolderFactureAsync(id, request);
                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new { success = true, data = facture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur soldage facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}/annuler")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> AnnulerFacture(Guid id)
        {
            try
            {
                var facture = await _factureService.AnnulerFactureAsync(id);
                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new { success = true, data = facture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _factureService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new { success = true, message = "Facture supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}