namespace Api_BuildTech.Controllers.Synchronisation
{
    public class CDCChange
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public string TableName { get; set; }
        public Guid RecordId { get; set; }
        public string Operation { get; set; } // INSERT, UPDATE, DELETE
        public string Direction { get; set; } // LOCAL_TO_REMOTE, REMOTE_TO_LOCAL
        public long Version { get; set; }
        public long? PreviousVersion { get; set; }
        public string? DataJSON { get; set; }
        public string? ChangedFields { get; set; }
        public bool IsSynced { get; set; }
        public int SyncAttempts { get; set; }
        public int MaxRetries { get; set; } = 3;
        public int Priority { get; set; } = 5;
        public DateTime CreatedDate { get; set; }
        public DateTime? SyncedDate { get; set; }
        public DateTime? NextRetryDate { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastErrorDate { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
        public string? SourceMachine { get; set; }
        public string? ConflictResolution { get; set; }
        public bool IsConflict { get; set; }
        public Guid? ConflictWith { get; set; }
    }

    /// <summary>
    /// Modèle pour la requête de synchronisation
    /// </summary>
    public class SyncRequest
    {
        public string ApiKey { get; set; }
        public Guid IdEntreprise { get; set; }
        public string TableName { get; set; }
        public Guid RecordId { get; set; }
        public string Operation { get; set; }
        public string? DataJSON { get; set; }
        public int Priority { get; set; } = 5;
        public string? SourceMachine { get; set; }
    }

    /// <summary>
    /// Modèle pour la réponse de synchronisation
    /// </summary>
    public class SyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid? ChangeId { get; set; }
        public bool HasConflict { get; set; }
        public ConflictInfo? Conflict { get; set; }
    }

    /// <summary>
    /// Informations sur un conflit
    /// </summary>
    public class ConflictInfo
    {
        public Guid ConflictId { get; set; }
        public string Field { get; set; }
        public string LocalValue { get; set; }
        public string RemoteValue { get; set; }
        public string ResolutionStrategy { get; set; }
    }

    /// <summary>
    /// Requête pour obtenir les changements en attente
    /// </summary>
    public class GetPendingChangesRequest
    {
        public string ApiKey { get; set; }
        public Guid IdEntreprise { get; set; }
        public DateTime Since { get; set; }
        public int MaxResults { get; set; } = 100;
        public string Direction { get; set; } = "REMOTE_TO_LOCAL";
    }

    /// <summary>
    /// Réponse avec la liste des changements
    /// </summary>
    public class PendingChangesResponse
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public List<CDCChange> Changes { get; set; } = new List<CDCChange>();
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistiques de synchronisation
    /// </summary>
    public class SyncStatistics
    {
        public Guid IdEntreprise { get; set; }
        public int TotalPending { get; set; }
        public int TotalSynced { get; set; }
        public int TotalFailed { get; set; }
        public int TotalConflicts { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public List<TableSyncInfo> TableStats { get; set; } = new List<TableSyncInfo>();
    }

    /// <summary>
    /// Statistiques par table
    /// </summary>
    public class TableSyncInfo
    {
        public string TableName { get; set; }
        public int PendingChanges { get; set; }
        public DateTime? LastSync { get; set; }
        public long LastVersion { get; set; }
    }

    /// <summary>
    /// Requête pour marquer comme synchronisé
    /// </summary>
    public class MarkSyncedRequest
    {
        public string ApiKey { get; set; }
        public Guid ChangeId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Conflit détecté
    /// </summary>
    public class Conflict
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public string TableName { get; set; }
        public Guid RecordId { get; set; }
        public string Field { get; set; }
        public string? LocalValue { get; set; }
        public string? RemoteValue { get; set; }
        public long LocalVersion { get; set; }
        public long RemoteVersion { get; set; }
        public DateTime LocalTimestamp { get; set; }
        public DateTime RemoteTimestamp { get; set; }
        public string ResolutionStrategy { get; set; } // LATEST_WINS, LOCAL_WINS, REMOTE_WINS, MANUAL, MERGE
        public string? ResolvedValue { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public Guid? ResolvedByUserId { get; set; }
        public DateTime DetectedDate { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Métadonnées de synchronisation
    /// </summary>
    public class SyncMetadata
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public DateTime LastSyncDate { get; set; }
        public DateTime? LastSuccessfulSyncDate { get; set; }
        public string Direction { get; set; }
        public int TotalSynced { get; set; }
        public int TotalFailed { get; set; }
        public int TotalConflicts { get; set; }
        public string TableName { get; set; }
        public long LastSyncVersion { get; set; }
        public int PendingChanges { get; set; }
        public bool IsSyncInProgress { get; set; }
        public DateTime? SyncStartDate { get; set; }
        public int? CurrentBatchNumber { get; set; }
    }
}
