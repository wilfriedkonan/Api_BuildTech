using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Paiments
{
    public class PaimentsService : DatabaseService
    {
        public PaimentsService(
            string connectionString,
            ILogger<PaimentsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<PaiementListResponse> GetAllAsync(DateTime? dateDebut = null, DateTime? dateFin = null)
        {
            var result = new PaiementListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("p");

                var dateFilter = "";
                if (dateDebut.HasValue && dateFin.HasValue)
                {
                    dateFilter = "AND p.Date BETWEEN @DateDebut AND @DateFin";
                }
                else if (dateDebut.HasValue)
                {
                    dateFilter = "AND p.Date >= @DateDebut";
                }
                else if (dateFin.HasValue)
                {
                    dateFilter = "AND p.Date <= @DateFin";
                }

                using var cmd = new SqlCommand($@"
                    SELECT p.Id, p.Designation, p.Montant, p.Date, p.Idclient, 
                           p.IdTypePaiment, p.IdEntreprise, p.Identifiant,
                           c.Nom + ' ' + ISNULL(c.Prenoms, '') AS NomClient,
                           tp.Designation AS TypePaiement
                    FROM PAIMENTS p
                    LEFT JOIN CLIENTS c ON p.Idclient = c.Id
                    LEFT JOIN TYPE_PAIEMENT tp ON p.IdTypePaiment = tp.Id
                    WHERE 1=1 {whereClause} {dateFilter}
                    ORDER BY p.Date DESC", conn);

                AddEntrepriseParameter(cmd);

                if (dateDebut.HasValue)
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                if (dateFin.HasValue)
                    cmd.Parameters.AddWithValue("@DateFin", dateFin.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var paiement = new PaiementDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Montant = reader.GetDecimal("Montant"),
                        Date = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        IdClient = ReadNullableGuid(reader, "Idclient"),
                        IdTypePaiement = ReadNullableGuid(reader, "IdTypePaiment"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(7),
                        NomClient = ReadNullableString(reader, "NomClient"),
                        TypePaiement = ReadNullableString(reader, "TypePaiement")
                    };

                    result.Paiements.Add(paiement);
                    result.TotalMontant += paiement.Montant ?? 0;
                }

                result.Total = result.Paiements.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération paiements");
                result.Success = false;
            }

            return result;
        }

        public async Task<PaiementDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("p");

                using var cmd = new SqlCommand($@"
                    SELECT p.Id, p.Designation, p.Montant, p.Date, p.Idclient, 
                           p.IdTypePaiment, p.IdEntreprise, p.Identifiant,
                           c.Nom + ' ' + ISNULL(c.Prenoms, '') AS NomClient,
                           tp.Designation AS TypePaiement
                    FROM PAIMENTS p
                    LEFT JOIN CLIENTS c ON p.Idclient = c.Id
                    LEFT JOIN TYPE_PAIEMENT tp ON p.IdTypePaiment = tp.Id
                    WHERE p.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new PaiementDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Montant = reader.GetDecimal("Montant"),
                        Date = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        IdClient = ReadNullableGuid(reader, "Idclient"),
                        IdTypePaiement = ReadNullableGuid(reader, "IdTypePaiment"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(7),
                        NomClient = ReadNullableString(reader, "NomClient"),
                        TypePaiement = ReadNullableString(reader, "TypePaiement")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération paiement {id}");
            }

            return null;
        }

        public async Task<PaiementDto?> CreateAsync(CreatePaiementRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO PAIMENTS (
                        Id, Designation, Montant, Date, Idclient, IdTypePaiment, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, @Montant, @Date, @IdClient, @IdTypePaiement, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Montant", request.Montant);
                cmd.Parameters.AddWithValue("@Date", request.Date);
                AddParameter(cmd, "@IdClient", request.IdClient);
                cmd.Parameters.AddWithValue("@IdTypePaiement", request.IdTypePaiement);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Paiement créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création paiement");
                return null;
            }
        }

        public async Task<PaiementDto?> UpdateAsync(Guid id, UpdatePaiementRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE PAIMENTS
                    SET Designation = COALESCE(@Designation, Designation),
                        Montant = COALESCE(@Montant, Montant),
                        Date = COALESCE(@Date, Date),
                        Idclient = COALESCE(@IdClient, Idclient),
                        IdTypePaiment = COALESCE(@IdTypePaiement, IdTypePaiment)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Montant", request.Montant);
                AddParameter(cmd, "@Date", request.Date);
                AddParameter(cmd, "@IdClient", request.IdClient);
                AddParameter(cmd, "@IdTypePaiement", request.IdTypePaiement);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour paiement {id}");
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
                    DELETE FROM PAIMENTS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Paiement supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression paiement {id}");
                return false;
            }
        }
    }
}