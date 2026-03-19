using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.DetailTransactions
{
    public class DetailTransactionsService : DatabaseService
    {
        public DetailTransactionsService(
            string connectionString,
            ILogger<DetailTransactionsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<DetailTransactionListResponse> GetByFactureAsync(Guid idFacture)
        {
            var result = new DetailTransactionListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, PrixUnitaire, PrixTotal, PrixVente, Quantité,
                           Specificite, DetailComposent, Position, IdFacture, IdArticle,
                           idTypeService, IdEntreprise, IdServeur, IdCuisinier, IdUser,
                           Etat, Date, DesignationAgent, EstExecuter, estSuite,
                           estDetaileComd, estSupprimer, EstModifier, EstAvarie,
                           AutorisationModif, PrixAchatUnitaire, domaineAricle, idDomaine
                    FROM DETAIL_TRANSACTIONS
                    WHERE IdFacture = @IdFacture 
                    AND ISNULL(estSupprimer, 0) = 0 {whereClause}
                    ORDER BY Position, Id", conn);

                cmd.Parameters.AddWithValue("@IdFacture", idFacture);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var detail = new DetailTransactionDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                        PrixTotal = reader.GetDecimal("PrixTotal"),
                        PrixVente = reader.GetDecimal("PrixVente"),
                        Quantite = reader.GetDecimal("Quantité"),
                        Specificite = ReadNullableString(reader, "Specificite"),
                        DetailComposent = ReadNullableString(reader, "DetailComposent"),
                        Position = ReadNullableInt(reader, "Position"),
                        IdFacture = ReadNullableGuid(reader, "IdFacture"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdTypeService = ReadNullableGuid(reader, "idTypeService"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdServeur = ReadNullableGuid(reader, "IdServeur"),
                        IdCuisinier = ReadNullableGuid(reader, "IdCuisinier"),
                        IdUser = ReadNullableGuid(reader, "IdUser"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Date = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                        DesignationAgent = ReadNullableString(reader, "DesignationAgent"),
                        EstExecuter = ReadNullableBool(reader, "EstExecuter"),
                        EstSuite = ReadNullableBool(reader, "estSuite"),
                        EstDetaileComd = ReadNullableBool(reader, "estDetaileComd"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        EstModifier = ReadNullableBool(reader, "EstModifier"),
                        EstAvarie = ReadNullableBool(reader, "EstAvarie"),
                        AutorisationModif = ReadNullableBool(reader, "AutorisationModif"),
                        PrixAchatUnitaire = reader.GetDecimal("PrixAchatUnitaire"),
                        DomaineAricle = ReadNullableString(reader, "domaineAricle"),
                        IdDomaine = ReadNullableGuid(reader, "idDomaine")
                    };

                    result.Details.Add(detail);
                    result.TotalMontant += detail.PrixTotal ?? 0;
                }

                result.Total = result.Details.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détails facture {idFacture}");
                result.Success = false;
            }

            return result;
        }

        public async Task<DetailTransactionDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, PrixUnitaire, PrixTotal, PrixVente, Quantité,
                           Specificite, DetailComposent, Position, IdFacture, IdArticle,
                           idTypeService, IdEntreprise, IdServeur, IdCuisinier, IdUser,
                           Etat, Date, DesignationAgent, EstExecuter, estSuite,
                           estDetaileComd, estSupprimer, EstModifier, EstAvarie,
                           AutorisationModif, PrixAchatUnitaire, domaineAricle, idDomaine
                    FROM DETAIL_TRANSACTIONS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new DetailTransactionDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                        PrixTotal = reader.GetDecimal("PrixTotal"),
                        PrixVente = reader.GetDecimal("PrixVente"),
                        Quantite = reader.GetDecimal("Quantité"),
                        Specificite = ReadNullableString(reader, "Specificite"),
                        DetailComposent = ReadNullableString(reader, "DetailComposent"),
                        Position = ReadNullableInt(reader, "Position"),
                        IdFacture = ReadNullableGuid(reader, "IdFacture"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdTypeService = ReadNullableGuid(reader, "idTypeService"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdServeur = ReadNullableGuid(reader, "IdServeur"),
                        IdCuisinier = ReadNullableGuid(reader, "IdCuisinier"),
                        IdUser = ReadNullableGuid(reader, "IdUser"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Date = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                        DesignationAgent = ReadNullableString(reader, "DesignationAgent"),
                        EstExecuter = ReadNullableBool(reader, "EstExecuter"),
                        EstSuite = ReadNullableBool(reader, "estSuite"),
                        EstDetaileComd = ReadNullableBool(reader, "estDetaileComd"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        EstModifier = ReadNullableBool(reader, "EstModifier"),
                        EstAvarie = ReadNullableBool(reader, "EstAvarie"),
                        AutorisationModif = ReadNullableBool(reader, "AutorisationModif"),
                        PrixAchatUnitaire = reader.GetDecimal("PrixAchatUnitaire"),
                        DomaineAricle = ReadNullableString(reader, "domaineAricle"),
                        IdDomaine = ReadNullableGuid(reader, "idDomaine")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détail transaction {id}");
            }

            return null;
        }

        public async Task<DetailTransactionDto?> CreateAsync(CreateDetailTransactionRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var prixTotal = request.PrixUnitaire * request.Quantite;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO DETAIL_TRANSACTIONS (
                        Id, Designation, PrixUnitaire, PrixTotal, PrixVente, Quantité,
                        Specificite, IdFacture, IdArticle, idTypeService, IdEntreprise,
                        IdServeur, Etat, Date, estSupprimer
                    )
                    VALUES (
                        @Id, @Designation, @PrixUnitaire, @PrixTotal, @PrixVente, @Quantite,
                        @Specificite, @IdFacture, @IdArticle, @IdTypeService, @IdEntreprise,
                        @IdServeur, 'EnCours', GETDATE(), 0
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@PrixUnitaire", request.PrixUnitaire);
                cmd.Parameters.AddWithValue("@PrixTotal", prixTotal);
                cmd.Parameters.AddWithValue("@PrixVente", request.PrixUnitaire);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                AddParameter(cmd, "@Specificite", request.Specificite);
                cmd.Parameters.AddWithValue("@IdFacture", request.IdFacture);
                cmd.Parameters.AddWithValue("@IdArticle", request.IdArticle);
                AddParameter(cmd, "@IdTypeService", request.IdTypeService);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@IdServeur", request.IdServeur);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Détail transaction créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création détail transaction");
                return null;
            }
        }

        public async Task<DetailTransactionDto?> UpdateAsync(Guid id, UpdateDetailTransactionRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                // Recalculer le prix total si prix ou quantité change
                var recalculerPrixTotal = request.PrixUnitaire.HasValue || request.Quantite.HasValue;

                string sql;
                if (recalculerPrixTotal)
                {
                    sql = $@"
                        UPDATE DETAIL_TRANSACTIONS
                        SET Designation = COALESCE(@Designation, Designation),
                            PrixUnitaire = COALESCE(@PrixUnitaire, PrixUnitaire),
                            Quantité = COALESCE(@Quantite, Quantité),
                            PrixTotal = COALESCE(@PrixUnitaire, PrixUnitaire) * COALESCE(@Quantite, Quantité),
                            Specificite = COALESCE(@Specificite, Specificite),
                            EstExecuter = COALESCE(@EstExecuter, EstExecuter),
                            EstModifier = COALESCE(@EstModifier, EstModifier),
                            EstAvarie = COALESCE(@EstAvarie, EstAvarie)
                        WHERE Id = @Id {whereClause}";
                }
                else
                {
                    sql = $@"
                        UPDATE DETAIL_TRANSACTIONS
                        SET Designation = COALESCE(@Designation, Designation),
                            Specificite = COALESCE(@Specificite, Specificite),
                            EstExecuter = COALESCE(@EstExecuter, EstExecuter),
                            EstModifier = COALESCE(@EstModifier, EstModifier),
                            EstAvarie = COALESCE(@EstAvarie, EstAvarie)
                        WHERE Id = @Id {whereClause}";
                }

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@PrixUnitaire", request.PrixUnitaire);
                AddParameter(cmd, "@Quantite", request.Quantite);
                AddParameter(cmd, "@Specificite", request.Specificite);
                AddParameter(cmd, "@EstExecuter", request.EstExecuter);
                AddParameter(cmd, "@EstModifier", request.EstModifier);
                AddParameter(cmd, "@EstAvarie", request.EstAvarie);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour détail transaction {id}");
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
                    UPDATE DETAIL_TRANSACTIONS
                    SET estSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Détail transaction supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression détail transaction {id}");
                return false;
            }
        }
    }
}