namespace Api_BuildTech.Controllers.DetailTransactions
{
    public class DetailTransactionDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? PrixTotal { get; set; }
        public decimal? PrixVente { get; set; }
        public decimal? Quantite { get; set; }
        public string? Specificite { get; set; }
        public string? DetailComposent { get; set; }
        public int? Position { get; set; }
        public Guid? IdFacture { get; set; }
        public Guid? IdArticle { get; set; }
        public Guid? IdTypeService { get; set; }
        public Guid? IdEntreprise { get; set; }
        public Guid? IdServeur { get; set; }
        public Guid? IdCuisinier { get; set; }
        public Guid? IdUser { get; set; }
        public string? Etat { get; set; }
        public DateTime? Date { get; set; }
        public string? DesignationAgent { get; set; }
        public bool? EstExecuter { get; set; }
        public bool? EstSuite { get; set; }
        public bool? EstDetaileComd { get; set; }
        public bool? EstSupprimer { get; set; }
        public bool? EstModifier { get; set; }
        public bool? EstAvarie { get; set; }
        public bool? AutorisationModif { get; set; }
        public decimal? PrixAchatUnitaire { get; set; }
        public string? DomaineAricle { get; set; }
        public Guid? IdDomaine { get; set; }
    }

    public class CreateDetailTransactionRequest
    {
        public string Designation { get; set; } = string.Empty;
        public decimal PrixUnitaire { get; set; }
        public decimal Quantite { get; set; }
        public Guid IdFacture { get; set; }
        public Guid IdArticle { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid? IdTypeService { get; set; }
        public Guid? IdServeur { get; set; }
        public string? Specificite { get; set; }
    }

    public class UpdateDetailTransactionRequest
    {
        public string? Designation { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? Quantite { get; set; }
        public string? Specificite { get; set; }
        public bool? EstExecuter { get; set; }
        public bool? EstModifier { get; set; }
        public bool? EstAvarie { get; set; }
    }

    public class DetailTransactionListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalMontant { get; set; }
        public List<DetailTransactionDto> Details { get; set; } = new();
    }
}