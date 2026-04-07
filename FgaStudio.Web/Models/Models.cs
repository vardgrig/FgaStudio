namespace FgaStudio.Web.Models;

public enum ConnectionType { Url, Database }

public enum AuthMethod { None, ApiToken, ClientCredentials }

public class ConnectionConfig
{
    public string Name { get; set; } = string.Empty;
    public ConnectionType Type { get; set; } = ConnectionType.Url;

    // URL mode
    public string? ApiUrl { get; set; }
    public AuthMethod AuthMethod { get; set; } = AuthMethod.None;

    // ApiToken auth
    public string? ApiToken { get; set; }

    // OAuth2 Client Credentials auth
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ApiAudience { get; set; }
    public string? ApiTokenIssuer { get; set; }

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
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AuthorizationModelViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? SchemaVersion { get; set; }
    public int TypeDefinitionCount { get; set; }
    public int ConditionCount { get; set; }
    public bool IsActive { get; set; }
}

public class AuthorizationModelDetailViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string? SchemaJson { get; set; }
    public List<TypeDefinitionNode> TypeDefinitions { get; set; } = [];
}

public class TypeDefinitionNode
{
    public string Type { get; set; } = string.Empty;
    public List<string> Relations { get; set; } = [];
}

public class TupleObjectNode
{
    public string Object { get; set; } = string.Empty;
    public string ObjectType => Object.Contains(':') ? Object[..Object.IndexOf(':')] : Object;
    public List<TupleRelationGroup> Relations { get; set; } = [];
    public int TupleCount => Relations.Sum(r => r.Users.Count);
}

public class TupleRelationGroup
{
    public string Relation { get; set; } = string.Empty;
    public List<string> Users { get; set; } = [];
}

public class TupleChangeViewModel
{
    public string User { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // "write" | "delete"
    public DateTime? Timestamp { get; set; }
}

public class TupleFilter
{
    public string? User { get; set; }
    public string? Relation { get; set; }
    public string? Object { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    // Used by the DB service for server-side ORDER BY.
    // Ignored by the HTTP service (OpenFGA API has no sort support).
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
}
