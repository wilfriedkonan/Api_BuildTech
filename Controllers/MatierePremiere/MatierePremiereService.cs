using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.MatierePremiere
{
    public class MatierePremiereService : DatabaseService
    {
        public MatierePremiereService(
            string connectionString,
            ILogger<MatierePremiereService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<MatierePremiereListResponse> GetAllAsync()
        {
            var result = new MatierePremiereListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m");

                using var cmd = new SqlCommand($@"
                    SELECT m.Id, m.Designation, m.quantite, m.quantiteInitial, 
                           m.PrixUnitaire, m.Montant, m.idUnite, m.IdEntreprise, 
                           m.EstSupprimer, u.Designation AS Unite
                    FROM MATIERE_PREMIERE m
                    LEFT JOIN UNITE_MESURES u ON m.idUnite = u.Id
                    WHERE ISNULL(m.EstSupprimer, 0) = 0 {whereClause}
                    ORDER BY m.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var matiere = new MatierePremiereDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Quantite = reader.GetDecimal("quantite"),
                        QuantiteInitial = reader.GetDecimal("quantiteInitial"),
                        PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                        Montant = reader.GetDecimal("Montant"),
                        IdUnite = ReadNullableGuid(reader, "idUnite"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        Unite = ReadNullableString(reader, "Unite")
                    };

                    result.Matieres.Add(matiere);
                    result.ValeurTotale += matiere.Montant ?? 0;
                }

                result.Total = result.Matieres.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération matières premières");
                result.Success = false;
            }

            return result;
        }

        public async Task<MatierePremiereDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m");

                using var cmd = new SqlCommand($@"
                    SELECT m.Id, m.Designation, m.quantite, m.quantiteInitial, 
                           m.PrixUnitaire, m.Montant, m.idUnite, m.IdEntreprise, 
                           m.EstSupprimer, u.Designation AS Unite
                    FROM MATIERE_PREMIERE m
                    LEFT JOIN UNITE_MESURES u ON m.idUnite = u.Id
                    WHERE m.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new MatierePremiereDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Quantite = reader.GetDecimal("quantite"),
                        QuantiteInitial = reader.GetDecimal("quantiteInitial"),
                        PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                        Montant = reader.GetDecimal("Montant"),
                        IdUnite = ReadNullableGuid(reader, "idUnite"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        Unite = ReadNullableString(reader, "Unite")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération matière première {id}");
            }

            return null;
        }

        public async Task<MatierePremiereDto?> CreateAsync(CreateMatierePremiereRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var montant = request.Quantite * request.PrixUnitaire;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO MATIERE_PREMIERE (
                        Id, Designation, quantite, quantiteInitial, PrixUnitaire, 
                        Montant, idUnite, IdEntreprise, EstSupprimer
                    )
                    VALUES (
                        @Id, @Designation, @Quantite, @QuantiteInitial, @PrixUnitaire, 
                        @Montant, @IdUnite, @IdEntreprise, 0
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@QuantiteInitial", request.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaire", request.PrixUnitaire);
                cmd.Parameters.AddWithValue("@Montant", montant);
                cmd.Parameters.AddWithValue("@IdUnite", request.IdUnite);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Matière première créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création matière première");
                return null;
            }
        }

        public async Task<MatierePremiereDto?> UpdateAsync(Guid id, UpdateMatierePremiereRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                // Recalculer montant si quantité ou prix change
                string sql = $@"
                    UPDATE MATIERE_PREMIERE
                    SET Designation = COALESCE(@Designation, Designation),
                        quantite = COALESCE(@Quantite, quantite),
                        PrixUnitaire = COALESCE(@PrixUnitaire, PrixUnitaire),
                        Montant = COALESCE(@Quantite, quantite) * COALESCE(@PrixUnitaire, PrixUnitaire),
                        idUnite = COALESCE(@IdUnite, idUnite)
                    WHERE Id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Quantite", request.Quantite);
                AddParameter(cmd, "@PrixUnitaire", request.PrixUnitaire);
                AddParameter(cmd, "@IdUnite", request.IdUnite);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour matière première {id}");
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
                    UPDATE MATIERE_PREMIERE
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Matière première supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression matière première {id}");
                return false;
            }
        }
    }
}