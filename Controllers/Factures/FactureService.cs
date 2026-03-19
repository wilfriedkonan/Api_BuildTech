using Api_BuildTech.Controllers.Factures;
using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Factures
{
    public class FactureService : DatabaseService
    {
        public FactureService(
            string connectionString,
            ILogger<FactureService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<FactureListResponse> GetAllAsync(DateTime? dateDebut, DateTime? dateFin)
        {
            var result = new FactureListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                var dateFilter = "";
                if (dateDebut.HasValue && dateFin.HasValue)
                {
                    dateFilter = "AND f.Date BETWEEN @DateDebut AND @DateFin";
                }

                using var cmd = new SqlCommand($@"
                    SELECT f.id, f.NumeroFacture, f.Designation, f.Date, f.Message,
                           f.Montant, f.MontantVerser, f.MonnaieRemis, f.RestApayer,
                           f.Remise, f.Solder, f.BeneficeSurFact, f.IdTable, f.Caisse,
                           f.Serveur, f.DesignationTable, f.Satue, f.DesignationInvervents,
                           f.IdPayement, f.idUtilisateur, f.IdClient, f.idFournisseur,
                           f.IdLivraison, f.IdEntreprise, f.idTypeService, f.idBlockCommandes,
                           f.Etat, f.EstAnnuler, f.iddentifient, f.EstSupprimer, f.ordre,
                           f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                           f.NomEnAttente, f.IdentifiantUser, f.IdentifiantTable,
                           f.OuvertureTable, f.TableBlocKReserv, f.Estreservation,
                           f.Dure, f.IdSession, f.identifiantSession, f.IdServeur
                    FROM FACTURE f
                    WHERE ISNULL(f.EstSupprimer, 0) = 0 {whereClause} {dateFilter}
                    ORDER BY f.Date DESC", conn);

                AddEntrepriseParameter(cmd);

                if (dateDebut.HasValue && dateFin.HasValue)
                {
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    cmd.Parameters.AddWithValue("@DateFin", dateFin.Value);
                }

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var facture = MapFactureFromReader(reader);
                    result.Factures.Add(facture);



                    // Par celle-ci :
                    if (facture.Montant != 0)
                        result.TotalMontant += facture.Montant;


                    if (facture.BeneficeSurFact.HasValue)
                        result.TotalBenefice += facture.BeneficeSurFact.Value;
                }

                result.Total = result.Factures.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures");
                result.Success = false;
            }

            return result;
        }

        public async Task<FactureListResponse> GetEnAttenteAsync()
        {
            var result = new FactureListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT f.id, f.NumeroFacture, f.Designation, f.Date, f.Message,
                           f.Montant, f.MontantVerser, f.MonnaieRemis, f.RestApayer,
                           f.Remise, f.Solder, f.BeneficeSurFact, f.IdTable, f.Caisse,
                           f.Serveur, f.DesignationTable, f.Satue, f.DesignationInvervents,
                           f.IdPayement, f.idUtilisateur, f.IdClient, f.idFournisseur,
                           f.IdLivraison, f.IdEntreprise, f.idTypeService, f.idBlockCommandes,
                           f.Etat, f.EstAnnuler, f.iddentifient, f.EstSupprimer, f.ordre,
                           f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                           f.NomEnAttente, f.IdentifiantUser, f.IdentifiantTable,
                           f.OuvertureTable, f.TableBlocKReserv, f.Estreservation,
                           f.Dure, f.IdSession, f.identifiantSession, f.IdServeur
                    FROM FACTURE f
                    WHERE f.EstEnattente = 1 
                      AND ISNULL(f.EstSupprimer, 0) = 0 
                      {whereClause}
                    ORDER BY f.Date DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Factures.Add(MapFactureFromReader(reader));
                }

                result.Total = result.Factures.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures en attente");
                result.Success = false;
            }

            return result;
        }

        public async Task<FactureListResponse> GetNonSoldeesAsync()
        {
            var result = new FactureListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT f.id, f.NumeroFacture, f.Designation, f.Date, f.Message,
                           f.Montant, f.MontantVerser, f.MonnaieRemis, f.RestApayer,
                           f.Remise, f.Solder, f.BeneficeSurFact, f.IdTable, f.Caisse,
                           f.Serveur, f.DesignationTable, f.Satue, f.DesignationInvervents,
                           f.IdPayement, f.idUtilisateur, f.IdClient, f.idFournisseur,
                           f.IdLivraison, f.IdEntreprise, f.idTypeService, f.idBlockCommandes,
                           f.Etat, f.EstAnnuler, f.iddentifient, f.EstSupprimer, f.ordre,
                           f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                           f.NomEnAttente, f.IdentifiantUser, f.IdentifiantTable,
                           f.OuvertureTable, f.TableBlocKReserv, f.Estreservation,
                           f.Dure, f.IdSession, f.identifiantSession, f.IdServeur
                    FROM FACTURE f
                    WHERE ISNULL(f.Solder, 0) = 0 
                      AND ISNULL(f.EstSupprimer, 0) = 0 
                      AND ISNULL(f.EstAnnuler, 0) = 0
                      {whereClause}
                    ORDER BY f.Date DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var facture = MapFactureFromReader(reader);
                    result.Factures.Add(facture);

                    if (facture.RestApayer.HasValue)
                        result.TotalMontant += facture.RestApayer.Value;
                }

                result.Total = result.Factures.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures non soldées");
                result.Success = false;
            }

            return result;
        }

        public async Task<FactureDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT f.id, f.NumeroFacture, f.Designation, f.Date, f.Message,
                           f.Montant, f.MontantVerser, f.MonnaieRemis, f.RestApayer,
                           f.Remise, f.Solder, f.BeneficeSurFact, f.IdTable, f.Caisse,
                           f.Serveur, f.DesignationTable, f.Satue, f.DesignationInvervents,
                           f.IdPayement, f.idUtilisateur, f.IdClient, f.idFournisseur,
                           f.IdLivraison, f.IdEntreprise, f.idTypeService, f.idBlockCommandes,
                           f.Etat, f.EstAnnuler, f.iddentifient, f.EstSupprimer, f.ordre,
                           f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                           f.NomEnAttente, f.IdentifiantUser, f.IdentifiantTable,
                           f.OuvertureTable, f.TableBlocKReserv, f.Estreservation,
                           f.Dure, f.IdSession, f.identifiantSession, f.IdServeur
                    FROM FACTURE f
                    WHERE f.id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapFactureFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération facture {id}");
            }

            return null;
        }

        public async Task<FactureDto?> CreateAsync(CreateFactureRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Calcul automatique RestApayer
                var montantVerser = request.MontantVerser ?? 0;
                var remise = request.Remise ?? 0;
                var restApayer = request.Montant - montantVerser - remise;
                var solder = restApayer <= 0;

                using var cmd = new SqlCommand(@"
                    INSERT INTO FACTURE (
                        id, NumeroFacture, Designation, Date, Message, Montant, 
                        MontantVerser, RestApayer, Remise, Solder, IdEntreprise,
                        IdTable, idUtilisateur, IdClient, Caisse, Serveur,
                        IdSession, EstSupprimer
                    )
                    VALUES (
                        @Id, @NumeroFacture, @Designation, @Date, @Message, @Montant,
                        @MontantVerser, @RestApayer, @Remise, @Solder, @IdEntreprise,
                        @IdTable, @IdUtilisateur, @IdClient, @Caisse, @Serveur,
                        @IdSession, 0
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                AddParameter(cmd, "@NumeroFacture", request.NumeroFacture);
                AddParameter(cmd, "@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Date", request.Date);
                AddParameter(cmd, "@Message", request.Message);
                cmd.Parameters.AddWithValue("@Montant", request.Montant);
                cmd.Parameters.AddWithValue("@MontantVerser", montantVerser);
                cmd.Parameters.AddWithValue("@RestApayer", restApayer);
                cmd.Parameters.AddWithValue("@Remise", remise);
                cmd.Parameters.AddWithValue("@Solder", solder);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@IdTable", request.IdTable);
                AddParameter(cmd, "@IdUtilisateur", request.IdUtilisateur);
                AddParameter(cmd, "@IdClient", request.IdClient);
                AddParameter(cmd, "@Caisse", request.Caisse);
                AddParameter(cmd, "@Serveur", request.Serveur);
                AddParameter(cmd, "@IdSession", request.IdSession);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Facture créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création facture");
                return null;
            }
        }

        public async Task<FactureDto?> UpdateAsync(Guid id, UpdateFactureRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE FACTURE
                    SET Designation = COALESCE(@Designation, Designation),
                        Message = COALESCE(@Message, Message),
                        Montant = COALESCE(@Montant, Montant),
                        Remise = COALESCE(@Remise, Remise),
                        IdClient = COALESCE(@IdClient, IdClient),
                        RestApayer = Montant - ISNULL(MontantVerser, 0) - ISNULL(COALESCE(@Remise, Remise), 0)
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Message", request.Message);
                AddParameter(cmd, "@Montant", request.Montant);
                AddParameter(cmd, "@Remise", request.Remise);
                AddParameter(cmd, "@IdClient", request.IdClient);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour facture {id}");
                return null;
            }
        }

        public async Task<FactureDto?> SolderFactureAsync(Guid id, SolderFactureRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE FACTURE
                    SET MontantVerser = @MontantVerser,
                        MonnaieRemis = @MonnaieRemis,
                        RestApayer = Montant - @MontantVerser - ISNULL(Remise, 0),
                        Solder = CASE WHEN (Montant - @MontantVerser - ISNULL(Remise, 0)) <= 0 THEN 1 ELSE 0 END,
                        IdPayement = @IdPayement
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@MontantVerser", request.MontantVerser);
                AddParameter(cmd, "@MonnaieRemis", request.MonnaieRemis);
                AddParameter(cmd, "@IdPayement", request.IdPayement);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Facture soldée: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur soldage facture {id}");
                return null;
            }
        }

        public async Task<FactureDto?> AnnulerFactureAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE FACTURE
                    SET EstAnnuler = 1,
                        Etat = 'Annulee'
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Facture annulée: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation facture {id}");
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
                    UPDATE FACTURE
                    SET EstSupprimer = 1
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Facture supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression facture {id}");
                return false;
            }
        }

        private FactureDto MapFactureFromReader(SqlDataReader reader)
        {
            return new FactureDto
            {
                Id = reader.GetGuid(0),
                NumeroFacture = ReadNullableString(reader, "NumeroFacture"),
                Designation = ReadNullableString(reader, "Designation"),
                Date = ReadNullableDateTime(reader, "Date"),
                Message = ReadNullableString(reader, "Message"),
                Montant = reader.GetDecimal("Montant"),
                MontantVerser = reader.GetDecimal("MontantVerser"),
                MonnaieRemis = reader.GetDecimal("MonnaieRemis"),
                RestApayer = reader.GetDecimal("RestApayer"),
                Remise = reader.GetDecimal("Remise"),
                Solder = ReadNullableBool(reader, "Solder"),
                BeneficeSurFact = reader.GetDecimal("BeneficeSurFact"),
                IdTable = ReadNullableGuid(reader, "IdTable"),
                Caisse = ReadNullableString(reader, "Caisse"),
                Serveur = ReadNullableString(reader, "Serveur"),
                DesignationTable = ReadNullableString(reader, "DesignationTable"),
                Satue = ReadNullableString(reader, "Satue"),
                DesignationInvervents = ReadNullableString(reader, "DesignationInvervents"),
                IdPayement = ReadNullableGuid(reader, "IdPayement"),
                IdUtilisateur = ReadNullableGuid(reader, "idUtilisateur"),
                IdClient = ReadNullableGuid(reader, "IdClient"),
                IdFournisseur = ReadNullableGuid(reader, "idFournisseur"),
                IdLivraison = ReadNullableGuid(reader, "IdLivraison"),
                IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                IdTypeService = ReadNullableGuid(reader, "idTypeService"),
                IdBlockCommandes = ReadNullableGuid(reader, "idBlockCommandes"),
                Etat = ReadNullableString(reader, "Etat"),
                EstAnnuler = ReadNullableBool(reader, "EstAnnuler"),
                Iddentifient = ReadNullableInt(reader, "iddentifient"),
                EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                Ordre = ReadNullableInt(reader, "ordre"),
                EstestCloturer = ReadNullableBool(reader, "estestCloturer"),
                EstEnattente = ReadNullableBool(reader, "EstEnattente"),
                DesignationAtttente = ReadNullableString(reader, "DesignationAtttente"),
                NomEnAttente = ReadNullableString(reader, "NomEnAttente"),
                IdentifiantUser = ReadNullableInt(reader, "IdentifiantUser"),
                IdentifiantTable = ReadNullableInt(reader, "IdentifiantTable"),
                OuvertureTable = ReadNullableString(reader, "OuvertureTable"),
                TableBlocKReserv = ReadNullableString(reader, "TableBlocKReserv"),
                Estreservation = ReadNullableBool(reader, "Estreservation"),
                Dure = ReadNullableString(reader, "Dure"),
                IdSession = ReadNullableGuid(reader, "IdSession"),
                IdentifiantSession = ReadNullableInt(reader, "identifiantSession"),
                IdServeur = ReadNullableGuid(reader, "IdServeur")
            };
        }
    }
}