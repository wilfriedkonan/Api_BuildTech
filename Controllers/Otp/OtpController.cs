using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Api_BuildTech.Controllers.Otp
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtpController : ControllerBase
    {
        private readonly OtpService _otpService;
        private readonly ILogger<OtpController> _logger;

        public OtpController(
            OtpService otpService,
            ILogger<OtpController> logger)
        {
            _otpService = otpService;
            _logger = logger;
        }

        /// <summary>
        /// Génère et envoie un OTP par email
        /// </summary>
        [HttpPost("generate")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateOtp([FromBody] GenerateOtpRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                {
                    return BadRequest(new { message = "Email requis" });
                }

                // Récupérer l'adresse IP du client
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var result = await _otpService.GenerateAndSendOtpAsync(
                    request.Email,
                    request.Purpose,
                    ipAddress
                );

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération OTP");
                return StatusCode(500, new { message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Valide un code OTP
        /// </summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
                {
                    return BadRequest(new { message = "Email et code requis" });
                }

                var result = await _otpService.ValidateOtpAsync(
                    request.Email,
                    request.Code,
                    request.Purpose
                );

                if (!result.Success || !result.IsValid)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur validation OTP");
                return StatusCode(500, new { message = "Erreur serveur" });
            }
        }
    }
}