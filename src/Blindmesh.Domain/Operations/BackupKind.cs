namespace Blindmesh.Domain.Operations;

/// <summary>
/// The kind of a backup of the product. The kind tells the operator what the
/// backup holds, so the restore rehearsal knows what it may compare.
/// </summary>
public enum BackupKind
{
    /// <summary>The backup of the database of the product.</summary>
    Database,

    /// <summary>The backup of the objects of the private bucket.</summary>
    Objects,
}
