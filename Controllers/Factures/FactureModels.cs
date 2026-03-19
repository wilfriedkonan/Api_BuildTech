namespace Api_BuildTech.Controllers.Factures
{
    public class FactureDto
    {
        public Guid Id { get; set; }
        public string? NumeroFacture { get; set; }
        public string? Designation { get; set; }
        public DateTime? Date { get; set; }
        public string? Message { get; set; }
        public decimal Montant { get; set; }
        public decimal? MontantVerser { get; set; }
        public decimal? MonnaieRemis { get; set; }
        public decimal? RestApayer { get; set; }
        public decimal? Remise { get; set; }
        public bool? Solder { get; set; }
        public decimal? BeneficeSurFact { get; set; }
        public Guid? IdTable { get; set; }
        public string? Caisse { get; set; }
        public string? Serveur { get; set; }
        public string? DesignationTable { get; set; }
        public string? Satue { get; set; }
        public string? DesignationInvervents { get; set; }
        public Guid? IdPayement { get; set; }
        public Guid? IdUtilisateur { get; set; }
        public Guid? IdClient { get; set; }
        public Guid? IdFournisseur { get; set; }
        public Guid? IdLivraison { get; set; }
        public Guid? IdEntreprise { get; set; }
        public Guid? IdTypeService { get; set; }
        public Guid? IdBlockCommandes { get; set; }
        public string? Etat { get; set; }
        public bool? EstAnnuler { get; set; }
        public int? Iddentifient { get; set; }
        public bool? EstSupprimer { get; set; }
        public int? Ordre { get; set; }
        public bool? EstestCloturer { get; set; }
        public bool? EstEnattente { get; set; }
        public string? DesignationAtttente { get; set; }
        public string? NomEnAttente { get; set; }
        public int? IdentifiantUser { get; set; }
        public int? IdentifiantTable { get; set; }
        public string? OuvertureTable { get; set; }
        public string? TableBlocKReserv { get; set; }
        public bool? Estreservation { get; set; }
        public string? Dure { get; set; }
        public Guid? IdSession { get; set; }
        public int? IdentifiantSession { get; set; }
        public Guid? IdServeur { get; set; }
    }

    public class CreateFactureRequest
    {
        public string? NumeroFacture { get; set; }
        public string? Designation { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string? Message { get; set; }
        public decimal Montant { get; set; }
        public decimal? MontantVerser { get; set; }
        public decimal? Remise { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid? IdTable { get; set; }
        public Guid? IdUtilisateur { get; set; }
        public Guid? IdClient { get; set; }
        public string? Caisse { get; set; }
        public string? Serveur { get; set; }
        public Guid? IdSession { get; set; }
    }

    public class UpdateFactureRequest
    {
        public string? Designation { get; set; }
        public string? Message { get; set; }
        public decimal? Montant { get; set; }
        public decimal? Remise { get; set; }
        public Guid? IdClient { get; set; }
    }

    public class SolderFactureRequest
    {
        public decimal MontantVerser { get; set; }
        public decimal? MonnaieRemis { get; set; }
        public Guid? IdPayement { get; set; }
    }

    public class FactureListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalMontant { get; set; }
        public decimal TotalBenefice { get; set; }
        public List<FactureDto> Factures { get; set; } = new();
    }
}