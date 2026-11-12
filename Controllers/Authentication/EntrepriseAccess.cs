using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Entreprises auxquelles un compte web (COMPTES_WEB) peut accéder : celles de son ORGANISATION qui sont
    /// actives ET dont l'abonnement est valide (ou autorisées manuellement). Même règle que l'ancien
    /// AuthService.ValidateEntrepriseAsync, appliquée à toute l'organisation. L'entreprise par défaut (IsDefault) vient en premier.
    /// </summary>
    public static class EntrepriseAccess
    {
        public static async Task<List<EntrepriseSummary>> ListValidesAsync(SqlConnection conn, Guid idOrganisation)
        {
            var list = new List<EntrepriseSummary>();

            using var cmd = new SqlCommand(@"
                SELECT e.Id, e.Designation, e.CodeEntreprise
                FROM ENTREPRISE e
                WHERE e.IdOrganisation = @Org
                  AND e.IsActive = 1
                  AND ISNULL(e.Etat, N'') <> N'Supprimer'
                  AND (e.Autorisation = 1
                       OR EXISTS (SELECT 1 FROM SUBSCRIPTIONS s
                                  WHERE s.IdEntreprise = e.Id
                                    AND s.Status = 'Active'
                                    AND s.EndDate > GETUTCDATE()))
                ORDER BY e.IsDefault DESC, e.Designation", conn);
            cmd.Parameters.AddWithValue("@Org", idOrganisation);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EntrepriseSummary
                {
                    Id = reader.GetGuid(0),
                    Designation = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CodeEntreprise = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
            return list;
        }

        /// <summary>Nombre d'entreprises ACTIVES (IsActive = 1) de l'organisation, abonnement valide ou non.</summary>
        public static async Task<int> CompterActivesAsync(SqlConnection conn, Guid idOrganisation)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM ENTREPRISE
                WHERE IdOrganisation = @Org AND IsActive = 1 AND ISNULL(Etat, N'') <> N'Supprimer'", conn);
            cmd.Parameters.AddWithValue("@Org", idOrganisation);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
    }
}
