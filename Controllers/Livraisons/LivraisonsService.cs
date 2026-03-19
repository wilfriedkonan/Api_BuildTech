using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Livraisons
{
    public class LivraisonsService : DatabaseService
    {
        public LivraisonsService(
            string connectionString,
            ILogger<LivraisonsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<LivraisonListResponse> GetAllAsync(bool? enCoursOnly = null)
        {
            var result = new LivraisonListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var enCoursFilter = enCoursOnly.HasValue && enCoursOnly.Value ?
                    "AND ISNULL(EstEnCours, 0) = 1 AND ISNULL(EstTerminer, 0) = 0" : "";

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, DesignationLivreur, Prix, Satut, TotalCommande, 
                           PrixTotal, IdLivreur, Date, Etat, EstEnCours, EstTerminer, 
                           DesignationBloc, IdBlockCommande, idFrais, IdEntreprise
                    FROM LIVRAISONS
                    WHERE ISNULL(Etat, 'Actif') = 'Actif' {whereClause} {enCoursFilter}
                    ORDER BY Date DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var livraison = new LivraisonDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        DesignationLivreur = ReadNullableString(reader, "DesignationLivreur"),
                        Prix = reader.GetDecimal("Prix"),
                        Satut = ReadNullableString(reader, "Satut"),
                        TotalCommande = ReadNullableInt(reader, "TotalCommande"),
                        PrixTotal = reader.GetDecimal("PrixTotal"),
                        IdLivreur = ReadNullableGuid(reader, "IdLivreur"),
                        Date = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstEnCours = ReadNullableBool(reader, "EstEnCours"),
                        EstTerminer = ReadNullableBool(reader, "EstTerminer"),
                        DesignationBloc = ReadNullableString(reader, "DesignationBloc"),
                        IdBlockCommande = ReadNullableGuid(reader, "IdBlockCommande"),
                        IdFrais = ReadNullableGuid(reader, "idFrais"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };

                    result.Livraisons.Add(livraison);
                    result.TotalPrix += livraison.Prix ?? 0;
                }

                result.Total = result.Livraisons.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération livraisons");
                result.Success = false;
            }

            return result;
        }

        public async Task<LivraisonDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, DesignationLivreur, Prix, Satut, TotalCommande, 
                           PrixTotal, IdLivreur, Date, Etat, EstEnCours, EstTerminer, 
                           DesignationBloc, IdBlockCommande, idFrais, IdEntreprise
                    FROM LIVRAISONS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new LivraisonDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        DesignationLivreur = ReadNullableString(reader, "DesignationLivreur"),
                        Prix = reader.GetDecimal("Prix"),
                        Satut = ReadNullableString(reader, "Satut"),
                        TotalCommande = ReadNullableInt(reader, "TotalCommande"),
                        PrixTotal = reader.GetDecimal("PrixTotal"),
                        IdLivreur = ReadNullableGuid(reader, "IdLivreur"),
                        Date = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstEnCours = ReadNullableBool(reader, "EstEnCours"),
                        EstTerminer = ReadNullableBool(reader, "EstTerminer"),
                        DesignationBloc = ReadNullableString(reader, "DesignationBloc"),
                        IdBlockCommande = ReadNullableGuid(reader, "IdBlockCommande"),
                        IdFrais = ReadNullableGuid(reader, "idFrais"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération livraison {id}");
            }

            return null;
        }

        public async Task<LivraisonDto?> CreateAsync(CreateLivraisonRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Récupérer nom livreur
                string? nomLivreur = null;
                using (var cmdLivreur = new SqlCommand(
                    "SELECT Designatin FROM LIVREUR WHERE Id = @IdLivreur", conn))
                {
                    cmdLivreur.Parameters.AddWithValue("@IdLivreur", request.IdLivreur);
                    nomLivreur = (string?)await cmdLivreur.ExecuteScalarAsync();
                }

                using var cmd = new SqlCommand(@"
                    INSERT INTO LIVRAISONS (
                        Id, Designation, DesignationLivreur, Prix, Satut, 
                        IdLivreur, Date, Etat, EstEnCours, EstTerminer, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, @DesignationLivreur, @Prix, 'EnCours', 
                        @IdLivreur, GETDATE(), 'Actif', 1, 0, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                AddParameter(cmd, "@DesignationLivreur", nomLivreur);
                cmd.Parameters.AddWithValue("@Prix", request.Prix);
                cmd.Parameters.AddWithValue("@IdLivreur", request.IdLivreur);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Livraison créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création livraison");
                return null;
            }
        }

        public async Task<LivraisonDto?> TerminerAsync(Guid id, TerminerLivraisonRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE LIVRAISONS
                    SET EstEnCours = 0,
                        EstTerminer = 1,
                        Satut = 'Terminé',
                        PrixTotal = COALESCE(@PrixTotal, PrixTotal)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@PrixTotal", request.PrixTotal);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Livraison terminée: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur terminer livraison {id}");
                return null;
            }
        }

        public async Task<LivraisonDto?> UpdateAsync(Guid id, UpdateLivraisonRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE LIVRAISONS
                    SET Designation = COALESCE(@Designation, Designation),
                        Prix = COALESCE(@Prix, Prix),
                        Satut = COALESCE(@Satut, Satut),
                        EstEnCours = COALESCE(@EstEnCours, EstEnCours),
                        EstTerminer = COALESCE(@EstTerminer, EstTerminer)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Prix", request.Prix);
                AddParameter(cmd, "@Satut", request.Satut);
                AddParameter(cmd, "@EstEnCours", request.EstEnCours);
                AddParameter(cmd, "@EstTerminer", request.EstTerminer);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour livraison {id}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE LIVRAISONS
                    SET Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Livraison désactivée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation livraison {id}");
                return false;
            }
        }
    }
}