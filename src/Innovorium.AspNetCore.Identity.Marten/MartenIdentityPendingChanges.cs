using System.Runtime.CompilerServices;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

internal enum MartenIdentityPendingChangeKind
{
    Relationship,
    AddRole,
    RemoveRole,
}

internal sealed class MartenIdentityPendingChanges<TDocument>
    where TDocument : class
{
    private static readonly ConditionalWeakTable<TDocument, MartenIdentityPendingChanges<TDocument>> Owners = new();

    private readonly List<Action<IDocumentSession>> _operations = [];
    private TDocument? _document;
    private IdentityError? _failure;
    private bool _validationObserved;

    public MartenIdentityPendingChangeKind Kind { get; private set; }

    public bool IsFor(TDocument document, MartenIdentityPendingChangeKind kind) =>
        ReferenceEquals(_document, document) && _operations.Count > 0 && Kind == kind;

    public void Begin(TDocument document, MartenIdentityPendingChangeKind kind)
    {
        if (_document is not null &&
            (!ReferenceEquals(_document, document) || _validationObserved || Kind != kind))
        {
            Clear();
        }

        _document = document;
        Kind = kind;
        Owners.Remove(document);
        Owners.Add(document, this);
    }

    public void Add(Action<IDocumentSession> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operations.Add(operation);
    }

    public void Reject(TDocument document, IdentityError failure)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(failure);
        Clear();
        _document = document;
        _failure = failure;
        Owners.Add(document, this);
    }

    public bool TryTakeFailure(TDocument document, out IdentityError? failure)
    {
        if (!ReferenceEquals(_document, document) || _failure is null)
        {
            failure = null;
            return false;
        }

        failure = _failure;
        Clear();
        return true;
    }

    public void Materialize(IDocumentSession session, TDocument document)
    {
        if (!ReferenceEquals(_document, document))
        {
            Clear();
            return;
        }

        foreach (var operation in _operations)
        {
            operation(session);
        }
    }

    public void Clear()
    {
        if (_document is not null)
        {
            Owners.Remove(_document);
        }

        _operations.Clear();
        _document = null;
        _failure = null;
        _validationObserved = false;
        Kind = default;
    }

    public static void ValidationStarting(TDocument document)
    {
        if (!Owners.TryGetValue(document, out var pending) || pending._operations.Count == 0)
        {
            return;
        }

        if (pending._validationObserved)
        {
            pending.Clear();
            return;
        }

        pending._validationObserved = true;
    }

    public static IdentityError? TakeFailure(TDocument document)
    {
        if (!Owners.TryGetValue(document, out var pending) ||
            !pending.TryTakeFailure(document, out var failure))
        {
            return null;
        }

        return failure;
    }
}

internal sealed class MartenPendingUserChangesValidator<TUser> : IUserValidator<TUser>
    where TUser : MartenIdentityUser
{
    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);
        var failure = MartenIdentityPendingChanges<TUser>.TakeFailure(user);
        if (failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(failure));
        }

        MartenIdentityPendingChanges<TUser>.ValidationStarting(user);
        return Task.FromResult(IdentityResult.Success);
    }
}

internal sealed class MartenPendingRoleChangesValidator<TRole> : IRoleValidator<TRole>
    where TRole : MartenIdentityRole
{
    public Task<IdentityResult> ValidateAsync(RoleManager<TRole> manager, TRole role)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(role);
        MartenIdentityPendingChanges<TRole>.ValidationStarting(role);
        return Task.FromResult(IdentityResult.Success);
    }
}
