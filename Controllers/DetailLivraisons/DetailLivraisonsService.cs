using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.DetailLivraisons
{
    public class DetailLivraisonsService : DatabaseService
    {
        public DetailLivraisonsService(
            string connectionString,
            ILogger<DetailLivraisonsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<DetailLivraisonListResponse> GetByLivraisonAsync(Guid idLivraison)
        {
            var result = new DetailLivraisonListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Date, Lieu, FraisLivraison, Etat, 
                           MotantFacture, TotalFacture, Satue, EstAnnuler, 
                           Justificatif, IdLivraison, IdFacture, IdEntreprise
                    FROM DETAIL_LIVRAISONS
                    WHERE IdLivraison = @IdLivraison 
                    AND ISNULL(EstAnnuler, 0) = 0 {whereClause}
                    ORDER BY Date DESC", conn);

                cmd.Parameters.AddWithValue("@IdLivraison", idLivraison);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var detail = new DetailLivraisonDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Date = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        Lieu = ReadNullableString(reader, "Lieu"),
                        FraisLivraison = reader.GetDecimal("FraisLivraison"),
                        Etat = ReadNullableString(reader, "Etat"),
                        MotantFacture = reader.GetDecimal("MotantFacture"),
                        TotalFacture = reader.GetDecimal("TotalFacture"),
                        Satue = ReadNullableString(reader, "Satue"),
                        EstAnnuler = ReadNullableBool(reader, "EstAnnuler"),
                        Justificatif = ReadNullableString(reader, "Justificatif"),
                        IdLivraison = ReadNullableGuid(reader, "IdLivraison"),
                        IdFacture = ReadNullableGuid(reader, "IdFacture"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };

                    result.Details.Add(detail);
                    result.TotalFrais += detail.FraisLivraison;
                }

                result.Total = result.Details.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détails livraison {idLivraison}");
                result.Success = false;
            }

            return result;
        }

        public async Task<DetailLivraisonDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Date, Lieu, FraisLivraison, Etat, 
                           MotantFacture, TotalFacture, Satue, EstAnnuler, 
                           Justificatif, IdLivraison, IdFacture, IdEntreprise
                    FROM DETAIL_LIVRAISONS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new DetailLivraisonDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Date = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        Lieu = ReadNullableString(reader, "Lieu"),
                        FraisLivraison = reader.GetDecimal("FraisLivraison"),
                        Etat = ReadNullableString(reader, "Etat"),
                        MotantFacture = reader.GetDecimal("MotantFacture"),
                        TotalFacture = reader.GetDecimal("TotalFacture"),
                        Satue = ReadNullableString(reader, "Satue"),
                        EstAnnuler = ReadNullableBool(reader, "EstAnnuler"),
                        Justificatif = ReadNullableString(reader, "Justificatif"),
                        IdLivraison = ReadNullableGuid(reader, "IdLivraison"),
                        IdFacture = ReadNullableGuid(reader, "IdFacture"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détail livraison {id}");
            }

            return null;
        }

        public async Task<DetailLivraisonDto?> CreateAsync(CreateDetailLivraisonRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var totalFacture = request.MotantFacture + request.FraisLivraison;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO DETAIL_LIVRAISONS (
                        Id, Designation, Date, Lieu, FraisLivraison, Etat, 
                        MotantFacture, TotalFacture, Satue, EstAnnuler, 
                        IdLivraison, IdFacture, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, GETDATE(), @Lieu, @FraisLivraison, 'Actif', 
                        @MotantFacture, @TotalFacture, 'EnCours', 0, 
                        @IdLivraison, @IdFacture, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Lieu", request.Lieu);
                cmd.Parameters.AddWithValue("@FraisLivraison", request.FraisLivraison);
                cmd.Parameters.AddWithValue("@MotantFacture", request.MotantFacture);
                cmd.Parameters.AddWithValue("@TotalFacture", totalFacture);
                cmd.Parameters.AddWithValue("@IdLivraison", request.IdLivraison);
                cmd.Parameters.AddWithValue("@IdFacture", request.IdFacture);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Détail livraison créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création détail livraison");
                return null;
            }
        }

        public async Task<DetailLivraisonDto?> UpdateAsync(Guid id, UpdateDetailLivraisonRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE DETAIL_LIVRAISONS
                    SET Designation = COALESCE(@Designation, Designation),
                        Lieu = COALESCE(@Lieu, Lieu),
                        FraisLivraison = COALESCE(@FraisLivraison, FraisLivraison),
                        Satue = COALESCE(@Satue, Satue),
                        EstAnnuler = COALESCE(@EstAnnuler, EstAnnuler),
                        Justificatif = COALESCE(@Justificatif, Justificatif)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Lieu", request.Lieu);
                AddParameter(cmd, "@FraisLivraison", request.FraisLivraison);
                AddParameter(cmd, "@Satue", request.Satue);
                AddParameter(cmd, "@EstAnnuler", request.EstAnnuler);
                AddParameter(cmd, "@Justificatif", request.Justificatif);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour détail livraison {id}");
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
                    UPDATE DETAIL_LIVRAISONS
                    SET EstAnnuler = 1,
                        Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Détail livraison annulé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation détail livraison {id}");
                return false;
            }
        }
    }
}