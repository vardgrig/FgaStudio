namespace FgaStudio.Web.Models;

public enum ConnectionType { Url, Database }

public class ConnectionConfig
{
    public string Name { get; set; } = string.Empty;
    public ConnectionType Type { get; set; } = ConnectionType.Url;

    // URL mode
    public string? ApiUrl { get; set; }
    public string? ApiToken { get; set; }

    // DB mode
    public string? ConnectionString { get; set; }

    // Active store context
    public string? StoreId { get; set; }
    public string? AuthorizationModelId { get; set; }
}

public class TupleKey
{
    public string User { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
}

public class TupleViewModel
{
    public string User { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
}

public class StoreViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AuthorizationModelViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class TupleFilter
{
    public string? User { get; set; }
    public string? Relation { get; set; }
    public string? Object { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
